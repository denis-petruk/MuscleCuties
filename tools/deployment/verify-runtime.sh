#!/usr/bin/env bash
# Verify exact serving code, candidate startup, approved synthetic API checks,
# and a bounded Lambda log window without printing responses or credentials.
set -euo pipefail
set +x
umask 077

[ "$#" -eq 3 ] || [ "$#" -eq 4 ] || exit 2
: "${MC_RELEASE_POLICY_FILE:?Protected release policy is required}"
python3 - "$@" <<'PY'
import json, os, pathlib, re, ssl, subprocess, sys, tempfile, urllib.error, urllib.parse, urllib.request

def fail(code): raise SystemExit(code)
def aws(*parts):
    try:
        result = subprocess.run(['aws', *parts, '--region', 'us-east-2', '--output', 'json'],
                                capture_output=True, text=True, timeout=60)
        if result.returncode: fail('RUNTIME_AWS_READ_FAILED')
        return json.loads(result.stdout)
    except (OSError, ValueError, subprocess.TimeoutExpired): fail('RUNTIME_AWS_READ_FAILED')
try: policy = json.loads(pathlib.Path(os.environ['MC_RELEASE_POLICY_FILE']).read_text())
except (OSError, ValueError): fail('RUNTIME_POLICY_MISSING')
function, expected, api_id = sys.argv[1:4]
qualifier = sys.argv[4] if len(sys.argv) == 5 else None
if function != policy.get('function_arn') or api_id != policy.get('api_id') or not re.fullmatch(r'[A-Za-z0-9+/]{43}=', expected): fail('RUNTIME_IDENTITY_MISMATCH')
if os.environ.get('CI_DEBUG_TRACE', 'false').lower() == 'true': fail('RUNTIME_DEBUG_TRACE_FORBIDDEN')
start = os.environ.get('MC_RELEASE_LOG_START_MS', '')
if not start.isdecimal(): fail('RUNTIME_LOG_WINDOW_MISSING')
config_args = ['lambda','get-function-configuration','--function-name',function]
if qualifier: config_args += ['--qualifier',qualifier]
elif policy.get('integration_mode') == 'alias': config_args += ['--qualifier',policy['alias']]
config = aws(*config_args)
if config.get('CodeSha256') != expected or config.get('State') != 'Active' or config.get('LastUpdateStatus') != 'Successful': fail('RUNTIME_CODE_MISMATCH')

if qualifier:
    # Direct candidate invocation does not cut over serving API traffic.
    event = {'version':'2.0','routeKey':'GET /health/live','rawPath':'/health/live',
             'rawQueryString':'','headers':{'host':'localhost'},'requestContext':{
             'accountId':policy['account_id'],'apiId':api_id,'domainName':'localhost',
             'http':{'method':'GET','path':'/health/live','protocol':'HTTP/1.1',
                     'sourceIp':'127.0.0.1','userAgent':'release-verifier'},
             'requestId':'synthetic-release-health','routeKey':'GET /health/live',
             'stage':'$default','time':'','timeEpoch':0},'isBase64Encoded':False}
    with tempfile.TemporaryDirectory() as tmp:
        payload = pathlib.Path(tmp) / 'event.json'; response = pathlib.Path(tmp) / 'response.json'
        payload.write_text(json.dumps(event))
        result = aws('lambda','invoke','--function-name',function,'--qualifier',qualifier,
                     '--cli-binary-format','raw-in-base64-out','--payload','fileb://'+str(payload),str(response))
        if result.get('StatusCode') != 200 or result.get('FunctionError'): fail('CANDIDATE_INVOKE_FAILED')
        try: code = json.loads(response.read_text()).get('statusCode')
        except (OSError, ValueError): fail('CANDIDATE_RESPONSE_INVALID')
        if code != 200: fail('CANDIDATE_STARTUP_FAILED')
else:
    if policy.get('synthetic_requests_approved') is not True: fail('SYNTHETIC_APPROVAL_MISSING')
    endpoint = policy.get('api_base_url','')
    parsed = urllib.parse.urlparse(endpoint)
    if parsed.scheme != 'https' or parsed.netloc != f'{api_id}.execute-api.us-east-2.amazonaws.com' or parsed.path not in ('','/'): fail('RUNTIME_API_TARGET_INVALID')
    token = os.environ.get('MC_SYNTHETIC_BEARER_TOKEN','')
    reads = policy.get('synthetic_reads'); write = policy.get('synthetic_write')
    if len(token) < 20 or not isinstance(reads,list) or not reads or not isinstance(write,dict): fail('SYNTHETIC_CONTRACT_MISSING')
    context = ssl.create_default_context(); context.minimum_version = ssl.TLSVersion.TLSv1_3
    def request(method,path,data=None,authenticated=True):
        if not isinstance(path,str) or not path.startswith('/') or '//' in path or '..' in path or '?' in path or '#' in path: fail('SYNTHETIC_PATH_INVALID')
        headers = {'User-Agent':'musclecuties-release-verifier'}
        if authenticated: headers['Authorization'] = 'Bearer '+token
        if data is not None: headers['Content-Type'] = 'application/json'
        try:
            with urllib.request.urlopen(urllib.request.Request(endpoint.rstrip('/')+path,data=data,headers=headers,method=method),timeout=15,context=context) as response: return response.status
        except urllib.error.HTTPError as error: return error.code
        except (OSError, ValueError): fail('SYNTHETIC_REQUEST_FAILED')
    if request('GET','/health/ready',authenticated=False) != 200: fail('READY_FAILED')
    for path in reads:
        if not isinstance(path,str) or not path.startswith('/api/v1/') or request('GET',path) != 200: fail('SYNTHETIC_READ_FAILED')
    if write.get('method') != 'POST' or not str(write.get('path','')).startswith('/api/v1/'): fail('SYNTHETIC_WRITE_INVALID')
    try: body = pathlib.Path(write['body_file']).read_bytes(); json.loads(body)
    except (OSError, KeyError, ValueError): fail('SYNTHETIC_WRITE_INVALID')
    if len(body) > 16384: fail('SYNTHETIC_WRITE_INVALID')
    if request('POST',write['path'],body) != 200: fail('SYNTHETIC_WRITE_FAILED')
    verify_path = write.get('verify_read_path')
    if not isinstance(verify_path,str) or not verify_path.startswith('/api/v1/') or request('GET',verify_path) != 200: fail('SYNTHETIC_WRITE_READ_FAILED')

group = policy.get('log_group')
if group != '/aws/lambda/'+policy.get('function_name',''): fail('RUNTIME_LOG_TARGET_INVALID')
events = aws('logs','filter-log-events','--log-group-name',group,'--start-time',start,'--filter-pattern','"START RequestId:"','--limit','100')
if not events.get('events'): fail('RUNTIME_LOG_EVIDENCE_MISSING')
critical = aws('logs','filter-log-events','--log-group-name',group,'--start-time',start,'--filter-pattern','?ERROR ?Exception ?NpgsqlException ?AccessDenied','--limit','100')
if critical.get('events'): fail('RUNTIME_CRITICAL_LOGS')
print('runtime_verified=true')
PY

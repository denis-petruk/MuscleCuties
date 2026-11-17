var response = http.get("http://127.0.0.1:5080/api/v1/dev/email-challenge/latest?email=" + encodeURIComponent(output.qaEmail) + "&purpose=1");
if (response.status !== 200) throw new Error("Development OTP channel unavailable: " + response.status);
output.otp = String(JSON.parse(response.body).code);

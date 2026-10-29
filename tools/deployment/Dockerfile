# Tags are version-specific but still require registry digest verification and
# digest pinning before this image may be used for an approved release.
FROM public.ecr.aws/aws-cli/aws-cli:2.27.49 AS awscli
FROM mcr.microsoft.com/dotnet/sdk:10.0.403

# The package validator uses Python's standard library and the ARM64 probe
# exercises the extracted package with the SDK. Exact apt package versions and
# image digests must be locked against an approved snapshot before production.
RUN apt-get update \
    && apt-get install -y --no-install-recommends python3 unzip zip ca-certificates \
    && rm -rf /var/lib/apt/lists/*
COPY --from=awscli /usr/local/aws-cli /usr/local/aws-cli
RUN ln -s /usr/local/aws-cli/v2/current/bin/aws /usr/local/bin/aws \
    && aws --version \
    && dotnet --version
WORKDIR /workspace

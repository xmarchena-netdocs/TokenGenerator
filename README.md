# AWS Token Generator

This tool generates AWS STS GetCallerIdentity signatures for authenticating service-to-service communication using AWS IAM roles.

## Prerequisites

- .NET 6.0 or higher
- AWS credentials configured in `~/.aws/credentials`
- AWS profiles with IAM roles configured

## AWS Credentials Setup

Your `~/.aws/credentials` file should contain profiles with the necessary role configurations. For example:

```ini
[rambo]
role_arn=arn:aws:iam::730335459224:role/sec-acl-api-svc
source_profile=730335459224_ND-AssumeRoleDev
region=us-west-2

[doc-metadata-api-svc]
region=us-west-2
role_arn=arn:aws:iam::730335459224:role/doc-metadata-api-svc
source_profile=730335459224_ND-AssumeRoleDev
```

## Usage

```bash
dotnet run <profile-name> <audience>
```

### Parameters

- `<profile-name>`: The name of the AWS profile from your `~/.aws/credentials` file
- `<audience>`: The target service audience for the token

### Examples

#### Generate token for sec-acl-api-svc

```bash
dotnet run rambo sec-acl-api-svc
```

#### Generate token for doc-metadata-api-svc

```bash
dotnet run doc-metadata-api-svc doc-metadata-api-svc
```

## Output

The tool will output:

1. **CLIENT SECRET**: The base64-encoded AWS signature that can be used as a client secret
2. **CURL COMMAND**: A ready-to-use curl command for obtaining an access token from the authentication service

### Example Output

```
Generating AWS Identity Signature for profile 'rambo'...
=== CLIENT SECRET ===
<base64-encoded-signature>
=====================

=== CURL COMMAND ===
curl -k -X POST "https://idp-auth-s2s-svc.lb.service/auth/v1/access_token" \
  -H "Content-Type: application/json" \
  -d '{
    "grant_type": "client_credentials",
    "client_id": "AWS",
    "client_secret": "<base64-encoded-signature>",
    "audience": "sec-acl-api-svc",
    "scope": "service.read.permissions"
  }'
====================
```

## Building

To build the project:

```bash
dotnet build
```

To run without building:

```bash
dotnet run <profile-name> <audience>
```

To build and run as a standalone executable:

```bash
dotnet publish -c Release
./bin/Release/net6.0/TokenGenerator <profile-name> <audience>
```

## Troubleshooting

### Profile Not Found Error

If you receive an error like:
```
Could not load '<profile-name>' AWS profile. Make sure the profile exists in your AWS credentials file (~/.aws/credentials).
```

**Solution**:
- Verify the profile name exists in your `~/.aws/credentials` file
- Ensure the profile has the correct `role_arn` and `source_profile` configured
- Check that the source profile has valid AWS credentials

### Missing Arguments Error

If you don't provide both required arguments, you'll see:
```
Usage: TokenGenerator <profile-name> <audience>
Example: TokenGenerator rambo sec-acl-api-svc
         TokenGenerator doc-metadata-api-svc doc-metadata-api-svc
```

**Solution**: Provide both the profile name and audience when running the tool.

## How It Works

1. The tool reads AWS credentials from the specified profile in your AWS credentials file
2. It uses the AWS STS GetCallerIdentity API to generate a signed request
3. The signature is encoded as base64 and can be used as a client secret for OAuth2 client credentials flow
4. The generated curl command can be used to exchange the signature for an access token

# AWS Token Generator

This tool generates bearer tokens for authenticating service-to-service communication using AWS IAM roles. It generates an AWS STS GetCallerIdentity signature and automatically exchanges it for an access token from the IDP service.

## Prerequisites

- .NET 8.0 or higher
- AWS credentials configured in `~/.aws/credentials`
- AWS profiles with IAM roles configured
- Access to the IDP authentication service

## AWS Credentials Setup

Your `~/.aws/credentials` file should contain profiles with the necessary role configurations. For example:

```ini
[sec-acl-api-svc]
role_arn=arn:aws:iam::767397921534:role/sec-acl-api-svc
source_profile=767397921534_ND-AssumeRoleQA
region=us-west-2

[doc-metadata-api-svc]
role_arn=arn:aws:iam::767397921534:role/doc-metadata-api-svc
source_profile=767397921534_ND-AssumeRoleQA
region=us-west-2

[doc-content-api-svc]
role_arn=arn:aws:iam::767397921534:role/doc-content-api-svc
source_profile=767397921534_ND-AssumeRoleQA
region=us-west-2
```

## Usage

```bash
dotnet run <profile-name> <audience> [scope]
```

### Parameters

- `<profile-name>`: The name of the AWS profile from your `~/.aws/credentials` file
- `<audience>`: The target service audience for the token
- `[scope]`: (Optional) Space-separated list of scopes. Default: `service.read`

### Examples

#### Generate token with default scope (service.read)

```bash
dotnet run doc-metadata-api-svc doc-metadata-api-svc
```

#### Generate token with single scope

```bash
dotnet run doc-metadata-api-svc doc-metadata-api-svc "service.create"
```

#### Generate token with multiple scopes (CRUD operations)

```bash
dotnet run doc-metadata-api-svc doc-metadata-api-svc "service.create service.read service.update service.delete"
```

#### Generate token for different services

```bash
dotnet run doc-content-api-svc doc-content-api-svc "service.read service.write"
dotnet run sec-acl-api-svc sec-acl-api-svc "service.read"
```

## Output

The tool will output:

1. **BEARER TOKEN**: The JWT access token that can be used directly in API calls
2. **Token Metadata**: Expiration time, token type, and scope information
3. **Usage Example**: A sample curl command showing how to use the token

### Example Output

```
Generating bearer token for profile 'doc-metadata-api-svc'...
Audience: doc-metadata-api-svc
Scope: service.create service.read service.update service.delete

=== BEARER TOKEN ===
eyJhbGciOiJSUzI1NiIsImtpZCI6ImE2MDhjZWE1LWZjZGEtNDQ0...

Token Type: Bearer
Expires: 2025-10-16T05:57:48Z
Scope: service.create service.read service.update service.delete

=== USAGE ===
curl -H "Authorization: Bearer eyJhbGciOiJSUzI1NiIs..." \
  http://doc-metadata-api-svc.lb.service/api/...
```

## Using the Token

Once you have the bearer token, use it in the `Authorization` header:

```bash
# Create a document
curl -X POST http://doc-metadata-api-svc.lb.service/v1/documents \
  -H "Authorization: Bearer <your-token>" \
  -H "Content-Type: application/json" \
  -H "X-ND-UserId: your-user-id" \
  -d '{
    "name": "My Document",
    "cabinetId": "CABINET-ID"
  }'

# Get a document
curl http://doc-metadata-api-svc.lb.service/v1/documents/DOC-ID \
  -H "Authorization: Bearer <your-token>"

# Update a document
curl -X PATCH http://doc-metadata-api-svc.lb.service/v1/documents/DOC-ID \
  -H "Authorization: Bearer <your-token>" \
  -H "Content-Type: application/json" \
  -d '{"archived": true}'

# Delete a document
curl -X DELETE http://doc-metadata-api-svc.lb.service/v1/documents/DOC-ID \
  -H "Authorization: Bearer <your-token>"
```

## Building

To build the project:

```bash
dotnet build
```

To run without building:

```bash
dotnet run <profile-name> <audience> [scope]
```

To build and run as a standalone executable:

```bash
dotnet publish -c Release
./bin/Release/net8.0/TokenGenerator <profile-name> <audience> [scope]
```

## Troubleshooting

### Profile Not Found Error

If you receive an error like:
```
Could not load '<profile-name>' AWS profile.
```

**Solution**:
- Verify the profile name exists in your `~/.aws/credentials` file
- Ensure the profile has the correct `role_arn` and `source_profile` configured
- Check that the source profile has valid AWS credentials

### Expired Token Error

If you receive an error like:
```
Error calling AssumeRole for role arn:aws:iam::...
The security token included in the request is expired
```

**Solution**:
- Refresh your AWS credentials by re-authenticating with AWS SSO
- Run `aws sso login --profile <your-profile>` if using SSO
- Verify your source profile credentials are not expired

### HTTP Error

If you receive an HTTP error when obtaining the access token:
```
HTTP Error: Failed to obtain access token. Status: 403
```

**Solution**:
- Verify the IAM role has the correct permissions
- Check that the audience matches the service name
- Ensure the requested scopes are authorized for your IAM role

### Missing Arguments Error

If you don't provide required arguments, you'll see:
```
Usage: TokenGenerator <profile-name> <audience> [scope]
```

**Solution**: Provide at minimum the profile name and audience when running the tool.

## How It Works

1. The tool reads AWS credentials from the specified profile in your AWS credentials file
2. It uses the AWS STS GetCallerIdentity API to generate a signed request (AWS V4 signature)
3. The signature is encoded as base64 and used as the `client_secret` in OAuth2 client credentials flow
4. The tool automatically makes an HTTP POST request to the IDP service at `https://idp-auth-s2s-svc.lb.service/auth/v1/access_token`
5. The IDP validates the AWS signature and returns a JWT bearer token
6. The bearer token is displayed and can be used directly in API requests

## Changes from Previous Version

### New Features
- ✅ **Direct bearer token generation**: No need to manually run curl commands
- ✅ **Parameterized scopes**: Specify custom scopes at runtime
- ✅ **Multiple scopes support**: Request multiple permissions in a single token
- ✅ **Automatic HTTP client**: Handles the token exchange automatically
- ✅ **TLS certificate bypass**: Works with self-signed certificates in local environments


## Comparison with Go Implementation

This C# tool provides the same functionality as the `service-to-service-cli` Go tool, with a simpler interface:

| Feature | C# TokenGenerator | Go service-to-service-cli |
|---------|------------------|---------------------------|
| Get bearer token | ✅ | ✅ |
| Custom scopes | ✅ | ✅ |
| Profile selection | ✅ Explicit parameter | Environment variable |
| Output format | Token + metadata | JSON response |
| Complexity | ~115 lines | ~500 lines |
| CLI framework | Built-in | Cobra |

Both tools are functionally equivalent and use the same underlying authentication flow.

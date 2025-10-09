using Amazon.Runtime;
using Amazon;
using Amazon.Runtime.CredentialManagement;
using NetDocuments.CallerIdentity;

// Parse command-line arguments
if (args.Length < 2)
{
    Console.WriteLine("Usage: TokenGenerator <profile-name> <audience>");
    Console.WriteLine("Example: TokenGenerator rambo sec-acl-api-svc");
    Console.WriteLine("         TokenGenerator doc-metadata-api-svc doc-metadata-api-svc");
    return 1;
}

string profileName = args[0];
string audience = args[1];

try
{
    Console.WriteLine($"Generating AWS Identity Signature for profile '{profileName}'...");

    // Load AWS credentials from the specified profile
    var chain = new CredentialProfileStoreChain();
    if (!chain.TryGetAWSCredentials(profileName, out AWSCredentials credentials))
    {
        throw new InvalidOperationException($"Could not load '{profileName}' AWS profile. Make sure the profile exists in your AWS credentials file (~/.aws/credentials).");
    }
    var region = RegionEndpoint.USWest2;

    var signer = new AwsV4IdentitySigner();
    var signature = await signer.Sign(credentials, region);

    if (signature == null)
    {
        throw new ArgumentNullException("Failed to retrieve signed GetCallerIdentity request.");
    }

    var identity = signature.ToBase64String();

    // Generate the complete CURL command
    Console.WriteLine("\n=== CURL COMMAND ===");
    Console.WriteLine($@"curl -k -X POST ""https://idp-auth-s2s-svc.lb.service/auth/v1/access_token"" \
  -H ""Content-Type: application/json"" \
  -d '{{
    ""grant_type"": ""client_credentials"",
    ""client_id"": ""AWS"",
    ""client_secret"": ""{identity}"",
    ""audience"": ""{audience}"",
    ""scope"": ""service.read.permissions""
  }}'");
    Console.WriteLine("====================");

    Console.WriteLine("\n=== CLIENT SECRET (Token) ===");
    Console.WriteLine(identity);

    return 0;
}
catch (Exception e)
{
    Console.WriteLine($"Error: {e.Message}");
    Console.WriteLine($"Stack trace: {e.StackTrace}");
    return 1;
}
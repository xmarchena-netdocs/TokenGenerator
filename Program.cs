using Amazon.Runtime;
using Amazon;
using Amazon.Runtime.CredentialManagement;
using NetDocuments.CallerIdentity;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

// Parse command-line arguments
if (args.Length < 2)
{
    Console.WriteLine("Usage: TokenGenerator <profile-name> <audience> [scope]");
    Console.WriteLine("Example: TokenGenerator doc-metadata-api-svc doc-metadata-api-svc");
    Console.WriteLine("         TokenGenerator doc-metadata-api-svc doc-metadata-api-svc \"service.read service.create\"");
    Console.WriteLine("");
    Console.WriteLine("Default scope: service.read");
    return 1;
}

string profileName = args[0];
string audience = args[1];
string scope = args.Length >= 3 ? args[2] : "service.read";

try
{
    Console.WriteLine($"Generating bearer token for profile '{profileName}'...");
    Console.WriteLine($"Audience: {audience}");
    Console.WriteLine($"Scope: {scope}");
    Console.WriteLine();

    // Load AWS credentials from the specified profile
    var chain = new CredentialProfileStoreChain();
    if (!chain.TryGetAWSCredentials(profileName, out AWSCredentials credentials))
    {
        throw new InvalidOperationException($"Could not load '{profileName}' AWS profile. Make sure the profile exists in your AWS credentials file (~/.aws/credentials).");
    }
    var region = RegionEndpoint.USWest2;

    // Generate AWS V4 signature
    var signer = new AwsV4IdentitySigner();
    var signature = await signer.Sign(credentials, region);

    if (signature == null)
    {
        throw new ArgumentNullException("Failed to retrieve signed GetCallerIdentity request.");
    }

    var identity = signature.ToBase64String();

    // Exchange signature for access token
    using var httpClient = new HttpClient(new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = (_, _, _, _) => true
    });

    var tokenRequest = new
    {
        grant_type = "client_credentials",
        client_id = "AWS",
        client_secret = identity,
        audience = audience,
        scope = scope
    };

    var response = await httpClient.PostAsJsonAsync(
        "https://idp-auth-s2s-svc.lb.service/auth/v1/access_token",
        tokenRequest
    );

    if (!response.IsSuccessStatusCode)
    {
        var errorContent = await response.Content.ReadAsStringAsync();
        throw new HttpRequestException($"Failed to obtain access token. Status: {response.StatusCode}, Error: {errorContent}");
    }

    var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();

    if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.AccessToken))
    {
        throw new InvalidOperationException("Failed to parse access token from response.");
    }

    // Output the bearer token
    Console.WriteLine("=== BEARER TOKEN ===");
    Console.WriteLine(tokenResponse.AccessToken);
    Console.WriteLine();
    Console.WriteLine($"Token Type: {tokenResponse.TokenType}");
    Console.WriteLine($"Expires: {tokenResponse.AccessTokenExpiration}");
    Console.WriteLine($"Scope: {scope}");
    Console.WriteLine();
    Console.WriteLine("=== USAGE ===");
    Console.WriteLine($"curl -H \"Authorization: Bearer {tokenResponse.AccessToken.Substring(0, Math.Min(50, tokenResponse.AccessToken.Length))}...\" \\");
    Console.WriteLine($"  http://{audience}.lb.service/api/...");

    return 0;
}
catch (HttpRequestException e)
{
    Console.WriteLine($"HTTP Error: {e.Message}");
    return 1;
}
catch (Exception e)
{
    Console.WriteLine($"Error: {e.Message}");
    return 1;
}

// Response model
record TokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("access_token_expiration")] string? AccessTokenExpiration,
    [property: JsonPropertyName("refresh_token")] string? RefreshToken,
    [property: JsonPropertyName("refresh_token_expiration")] string? RefreshTokenExpiration,
    [property: JsonPropertyName("token_type")] string TokenType
);

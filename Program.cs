using Amazon.Runtime;
using Amazon;
using Amazon.Runtime.CredentialManagement;
using NetDocuments.CallerIdentity;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

// Parse command-line arguments
// Check for --token-only flag and filter it out from arguments
bool tokenOnly = args.Contains("--token-only");
string[] filteredArgs = args.Where(arg => arg != "--token-only").ToArray();

if (filteredArgs.Length < 2)
{
    Console.WriteLine("Usage: TokenGenerator <profile-name> <audience> [scope] [--token-only]");
    Console.WriteLine("Example: TokenGenerator doc-metadata-api-svc doc-metadata-api-svc");
    Console.WriteLine("         TokenGenerator doc-metadata-api-svc doc-metadata-api-svc \"service.read service.create\"");
    Console.WriteLine("         TokenGenerator doc-metadata-api-svc doc-metadata-api-svc --token-only");
    Console.WriteLine("");
    Console.WriteLine("Options:");
    Console.WriteLine("  --token-only    Output only the raw bearer token (no headers, metadata, or usage info)");
    Console.WriteLine("");
    Console.WriteLine("Default scope: service.read");
    return 1;
}

string profileName = filteredArgs[0];
string audience = filteredArgs[1];
string scope = filteredArgs.Length >= 3 ? filteredArgs[2] : "service.read";

try
{
    if (!tokenOnly)
    {
        Console.WriteLine($"Generating bearer token for profile '{profileName}'...");
        Console.WriteLine($"Audience: {audience}");
        Console.WriteLine($"Scope: {scope}");
        Console.WriteLine();
    }

    // Load AWS credentials from the specified profile
    var chain = new CredentialProfileStoreChain();
    if (!chain.TryGetAWSCredentials(profileName, out AWSCredentials credentials))
    {
        throw new InvalidOperationException($"Could not load '{profileName}' AWS profile. Make sure the profile exists in your AWS credentials file (~/.aws/credentials).");
    }
    var region = RegionEndpoint.USWest2;

    // Generate AWS V4 signature
    var signer = new AwsV4IdentitySigner();

    dynamic? signature = null;
    try
    {
        signature = await signer.Sign(credentials, region);
    }
    catch (Exception ex)
    {
        throw new InvalidOperationException(
            $"Failed to sign AWS GetCallerIdentity request. " +
            $"This typically happens when assuming a role fails. " +
            $"Profile: {profileName}, Region: {region.SystemName}. " +
            $"Error: {ex.Message}",
            ex);
    }

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
    if (tokenOnly)
    {
        Console.WriteLine(tokenResponse.AccessToken);
    }
    else
    {
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
    }

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

    // Print inner exception details if they exist
    if (e.InnerException != null)
    {
        Console.WriteLine($"Inner Exception: {e.InnerException.Message}");

        // If there's an even deeper exception, show it too
        if (e.InnerException.InnerException != null)
        {
            Console.WriteLine($"Root Cause: {e.InnerException.InnerException.Message}");
        }
    }

    // Print stack trace for debugging
    Console.WriteLine();
    Console.WriteLine("Stack Trace:");
    Console.WriteLine(e.StackTrace);

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

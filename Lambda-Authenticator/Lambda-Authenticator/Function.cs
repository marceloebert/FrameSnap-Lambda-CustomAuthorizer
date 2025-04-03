using Amazon.Lambda.Core;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;
using System.Collections.Generic;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace Lambda_Authenticator;

public class Function
{
    private const string CognitoJwksUrl = "https://cognito-idp.us-east-1.amazonaws.com/us-east-1_Srf0ZBtbc/.well-known/jwks.json";
    private static readonly HttpClient HttpClient = new HttpClient();

    /// <summary>
    /// Main Lambda Handler for processing requests.
    /// </summary>
    /// <param name="request">The event for the Lambda function handler to process.</param>
    /// <param name="context">The ILambdaContext that provides methods for logging and describing the Lambda environment.</param>
    /// <returns>A policy document in the required format for API Gateway.</returns>
    public async Task<object> FunctionHandler(Dictionary<string, object> request, ILambdaContext context)
    {
        context.Logger.LogLine($"Received request: {JsonSerializer.Serialize(request)}");

        if (!request.ContainsKey("headers") || request["headers"] is not JsonElement headersElement)
        {
            context.Logger.LogLine("Missing or invalid headers in the request.");
            throw new UnauthorizedAccessException("Unauthorized. Token is required.");
        }

        // Extract Authorization header
        string authorizationHeader = null;
        if (headersElement.TryGetProperty("Authorization", out var authElement))
        {
            authorizationHeader = authElement.GetString();
        }

        if (string.IsNullOrEmpty(authorizationHeader))
        {
            context.Logger.LogLine("Missing Authorization header.");
            throw new UnauthorizedAccessException("Unauthorized. Token is required.");
        }

        string token = authorizationHeader.Replace("Bearer ", "");
        context.Logger.LogLine($"Authorization token: {token}");

        // Validate the JWT Token
        bool isValid = await ValidateJwtToken(token, context);
        if (!isValid)
        {
            context.Logger.LogLine("Invalid token received.");
            throw new UnauthorizedAccessException("Unauthorized. Invalid token.");
        }

        string methodArn = request["methodArn"]?.ToString();
        context.Logger.LogLine($"Token is valid. Generating policy for methodArn: {methodArn}");

        return GeneratePolicy("user", "Allow", methodArn);
    }

    /// <summary>
    /// Validates the JWT Token using Cognito's JWKS endpoint.
    /// </summary>
    /// <param name="token">JWT token to validate.</param>
    /// <param name="context">Lambda context for logging.</param>
    /// <returns>True if the token is valid, otherwise false.</returns>
    private async Task<bool> ValidateJwtToken(string token, ILambdaContext context)
    {
        try
        {
            context.Logger.LogLine("Fetching JWKS from Cognito...");
            var discoveryResponse = await HttpClient.GetStringAsync(CognitoJwksUrl);
            context.Logger.LogLine($"JWKS Response: {discoveryResponse}");
            var jsonWebKeySet = new JsonWebKeySet(discoveryResponse);
            var tokenHandler = new JwtSecurityTokenHandler();

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = jsonWebKeySet.Keys,
                ValidateIssuer = true,
                ValidateAudience = false, // Skip direct audience validation
                ValidIssuer = "https://cognito-idp.us-east-1.amazonaws.com/us-east-1_Srf0ZBtbc",
                ValidateLifetime = true
            };

            context.Logger.LogLine("Validating token...");
            var claimsPrincipal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);

            // Tenta pegar client_id; se não achar, tenta aud
            var clientId = claimsPrincipal.FindFirst("client_id")?.Value
                        ?? claimsPrincipal.FindFirst("aud")?.Value;

            context.Logger.LogLine($"Extracted clientId (or aud): {clientId}");

            if (clientId != "41vb4ljgmf4bhb8ogafjdjscst")
            {
                context.Logger.LogLine($"Invalid client_id: {clientId}");
                return false;
            }

            context.Logger.LogLine("Token is valid.");
            return true;
        }
        catch (SecurityTokenException ex)
        {
            context.Logger.LogLine($"Token validation failed: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            context.Logger.LogLine($"Unexpected error: {ex.Message}");
            return false;
        }
    }


    /// <summary>
    /// Generates an IAM policy document.
    /// </summary>
    /// <param name="principalId">The ID of the user.</param>
    /// <param name="effect">Allow or Deny.</param>
    /// <param name="resource">The ARN of the API Gateway method.</param>
    /// <returns>A policy document.</returns>
    private Dictionary<string, object> GeneratePolicy(string principalId, string effect, string resource)
    {
        var policyDocument = new Dictionary<string, object>
        {
            { "Version", "2012-10-17" },
            { "Statement", new List<Dictionary<string, string>>
                {
                    new Dictionary<string, string>
                    {
                        { "Action", "execute-api:Invoke" },
                        { "Effect", effect },
                        { "Resource", resource }
                    }
                }
            }
        };

        return new Dictionary<string, object>
        {
            { "principalId", principalId },
            { "policyDocument", policyDocument }
        };
    }
}

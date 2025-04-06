using Amazon.Lambda.Core;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;
using System.Collections.Generic;
using System.Linq;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace Lambda_Authenticator;

public class Function
{
    private const string CognitoJwksUrl = "https://cognito-idp.us-east-1.amazonaws.com/us-east-1_yc6M649rp/.well-known/jwks.json";
    private static readonly HttpClient HttpClient = new HttpClient();

    public async Task<object> FunctionHandler(Dictionary<string, object> request, ILambdaContext context)
    {
        context.Logger.LogLine($"Received request: {JsonSerializer.Serialize(request)}");

        if (!request.ContainsKey("headers") || request["headers"] is not JsonElement headersElement)
        {
            context.Logger.LogLine("Missing or invalid headers in the request.");
            throw new UnauthorizedAccessException("Unauthorized. Token is required.");
        }

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
                ValidateAudience = false,
                ValidIssuer = "https://cognito-idp.us-east-1.amazonaws.com/us-east-1_yc6M649rp",
                ValidateLifetime = true
            };

            context.Logger.LogLine("Validating token...");
            var claimsPrincipal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);

            var clientId = claimsPrincipal.FindFirst("client_id")?.Value
                        ?? claimsPrincipal.FindFirst("aud")?.Value;

            context.Logger.LogLine($"Extracted clientId (or aud): {clientId}");

            if (clientId != "6ghio8qtfebthof3sbch5d6c7c")
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

    private Dictionary<string, object> GeneratePolicy(string principalId, string effect, string methodArn)
    {
        var arnParts = methodArn.Split(':'); // arn:aws:execute-api:{region}:{account}:{apiId}/{stage}/{method}/{resourcePath}
        var region = arnParts[3];
        var accountId = arnParts[4];
        var apiGatewayArnParts = arnParts[5].Split('/');
        var restApiId = apiGatewayArnParts[0];
        var stage = apiGatewayArnParts[1];

        // Libera tudo em vídeos com qualquer método: GET, POST, etc.
        var wildcardArn = $"arn:aws:execute-api:{region}:{accountId}:{restApiId}/{stage}/*/videos/*";

        var policyDocument = new Dictionary<string, object>
        {
            { "Version", "2012-10-17" },
            { "Statement", new List<Dictionary<string, string>>
                {
                    new Dictionary<string, string>
                    {
                        { "Action", "execute-api:Invoke" },
                        { "Effect", effect },
                        { "Resource", wildcardArn }
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

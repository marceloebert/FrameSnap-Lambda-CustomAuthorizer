using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.TestUtilities;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text;
using Xunit;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using Lambda_Authenticator;
using System.Reflection;
using System.Security.Cryptography;

namespace Lambda.Authenticator.Tests
{
    public class FunctionTests
    {
        private readonly Function _function;
        private readonly TestLambdaContext _context;
        private readonly string _validClientId = "24aqngkfau1vjae4q4dqnu7bob";
        private readonly SecurityKey _signingKey;
        private readonly SigningCredentials _signingCredentials;

        public FunctionTests()
        {
            _function = new Function();
            _context = new TestLambdaContext();

            // Gerar chave RSA para testes
            var rsa = RSA.Create();
            _signingKey = new RsaSecurityKey(rsa);
            _signingCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.RsaSha256);
        }

        private string GenerateValidToken()
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var now = DateTime.UtcNow;

            var claims = new[]
            {
                new Claim("sub", _validClientId),
                new Claim("token_use", "access"),
                new Claim("scope", "aws.cognito.signin.user.admin"),
                new Claim("auth_time", now.ToString("o")),
                new Claim("iss", "https://cognito-idp.us-east-1.amazonaws.com/us-east-1_WkWACahpJ"),
                new Claim("exp", now.AddHours(1).ToString("o")),
                new Claim("iat", now.ToString("o")),
                new Claim("jti", Guid.NewGuid().ToString()),
                new Claim("client_id", _validClientId),
                new Claim("username", _validClientId)
            };

            var token = new JwtSecurityToken(
                issuer: "https://cognito-idp.us-east-1.amazonaws.com/us-east-1_WkWACahpJ",
                audience: _validClientId,
                claims: claims,
                expires: now.AddHours(1),
                signingCredentials: _signingCredentials
            );

            return tokenHandler.WriteToken(token);
        }

        [Fact]
        public async Task FunctionHandler_WithMissingHeaders_ThrowsUnauthorizedAccessException()
        {
            // Arrange
            var request = new Dictionary<string, object>
            {
                { "methodArn", "arn:aws:execute-api:us-east-1:123456789012:api-id/stage/GET/videos/123" }
            };

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _function.FunctionHandler(request, _context));
        }

        [Fact]
        public async Task FunctionHandler_WithMissingAuthorizationHeader_ThrowsUnauthorizedAccessException()
        {
            // Arrange
            var headers = new Dictionary<string, string>();
            var request = new Dictionary<string, object>
            {
                { "headers", JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(headers)) },
                { "methodArn", "arn:aws:execute-api:us-east-1:123456789012:api-id/stage/GET/videos/123" }
            };

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _function.FunctionHandler(request, _context));
        }

        [Fact]
        public async Task FunctionHandler_WithEmptyAuthorizationHeader_ThrowsUnauthorizedAccessException()
        {
            // Arrange
            var headers = new Dictionary<string, string>
            {
                { "Authorization", "" }
            };

            var request = new Dictionary<string, object>
            {
                { "headers", JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(headers)) },
                { "methodArn", "arn:aws:execute-api:us-east-1:123456789012:api-id/stage/GET/videos/123" }
            };

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _function.FunctionHandler(request, _context));
        }

        [Fact]
        public async Task FunctionHandler_WithInvalidToken_ThrowsUnauthorizedAccessException()
        {
            // Arrange
            var headers = new Dictionary<string, string>
            {
                { "Authorization", "Bearer invalid-token" }
            };

            var request = new Dictionary<string, object>
            {
                { "headers", JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(headers)) },
                { "methodArn", "arn:aws:execute-api:us-east-1:123456789012:api-id/stage/GET/videos/123" }
            };

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _function.FunctionHandler(request, _context));
        }

        [Fact]
        public void GeneratePolicy_ReturnsCorrectPolicy()
        {
            // Arrange
            var methodArn = "arn:aws:execute-api:us-east-1:123456789012:api-id/stage/GET/videos/123";

            // Act
            var result = _function.GetType()
                .GetMethod("GeneratePolicy", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(_function, new object[] { "user", "Allow", methodArn }) as Dictionary<string, object>;

            // Assert
            Assert.NotNull(result);
            Assert.Equal("user", result["principalId"]);
            var policyDocument = result["policyDocument"] as Dictionary<string, object>;
            Assert.NotNull(policyDocument);
            Assert.Equal("2012-10-17", policyDocument["Version"]);
            var statements = policyDocument["Statement"] as List<Dictionary<string, string>>;
            Assert.NotNull(statements);
            Assert.Single(statements);
            var statement = statements[0];
            Assert.Contains("arn:aws:execute-api:us-east-1:123456789012:api-id/stage/*/videos/*", statement["Resource"]);
        }

        [Fact]
        public async Task FunctionHandler_WithInvalidMethodArn_ThrowsException()
        {
            // Arrange
            var headers = new Dictionary<string, string>
            {
                { "Authorization", "Bearer invalid-token" }
            };

            var request = new Dictionary<string, object>
            {
                { "headers", JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(headers)) },
                { "methodArn", "invalid:arn" }
            };

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _function.FunctionHandler(request, _context));
        }

        [Fact]
        public async Task FunctionHandler_WithNullMethodArn_ThrowsException()
        {
            // Arrange
            var headers = new Dictionary<string, string>
            {
                { "Authorization", "Bearer invalid-token" }
            };

            var request = new Dictionary<string, object>
            {
                { "headers", JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(headers)) }
            };

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _function.FunctionHandler(request, _context));
        }

        [Fact]
        public async Task FunctionHandler_WithInvalidHeadersType_ThrowsException()
        {
            // Arrange
            var request = new Dictionary<string, object>
            {
                { "headers", "invalid-headers" },
                { "methodArn", "arn:aws:execute-api:us-east-1:123456789012:api-id/stage/GET/videos/123" }
            };

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _function.FunctionHandler(request, _context));
        }

        [Fact]
        public async Task FunctionHandler_WithNullHeaders_ThrowsException()
        {
            // Arrange
            var request = new Dictionary<string, object>
            {
                { "headers", null! },
                { "methodArn", "arn:aws:execute-api:us-east-1:123456789012:api-id/stage/GET/videos/123" }
            };

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _function.FunctionHandler(request, _context));
        }

        [Fact]
        public async Task FunctionHandler_WithInvalidAuthorizationHeaderType_ThrowsException()
        {
            // Arrange
            var request = new Dictionary<string, object>
            {
                { "headers", new Dictionary<string, object> { { "Authorization", 123 } } },
                { "methodArn", "arn:aws:execute-api:us-east-1:123456789012:api-id/stage/GET/videos/123" }
            };

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _function.FunctionHandler(request, _context));
        }

        [Fact]
        public async Task FunctionHandler_WithMalformedToken_ThrowsException()
        {
            // Arrange
            var headers = new Dictionary<string, string>
            {
                { "Authorization", "Bearer malformed.token.with.dots" }
            };

            var request = new Dictionary<string, object>
            {
                { "headers", JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(headers)) },
                { "methodArn", "arn:aws:execute-api:us-east-1:123456789012:api-id/stage/GET/videos/123" }
            };

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _function.FunctionHandler(request, _context));
        }

        [Fact]
        public async Task FunctionHandler_WithExpiredToken_ThrowsException()
        {
            // Arrange
            var tokenHandler = new JwtSecurityTokenHandler();
            var now = DateTime.UtcNow;

            var claims = new[]
            {
                new Claim("sub", _validClientId),
                new Claim("exp", now.AddHours(-1).ToString("o")), // Token expirado
                new Claim("client_id", _validClientId)
            };

            var token = new JwtSecurityToken(
                issuer: "https://cognito-idp.us-east-1.amazonaws.com/us-east-1_WkWACahpJ",
                audience: _validClientId,
                claims: claims,
                expires: now.AddHours(-1),
                signingCredentials: _signingCredentials
            );

            var expiredToken = tokenHandler.WriteToken(token);

            var headers = new Dictionary<string, string>
            {
                { "Authorization", $"Bearer {expiredToken}" }
            };

            var request = new Dictionary<string, object>
            {
                { "headers", JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(headers)) },
                { "methodArn", "arn:aws:execute-api:us-east-1:123456789012:api-id/stage/GET/videos/123" }
            };

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _function.FunctionHandler(request, _context));
        }

        [Fact]
        public async Task FunctionHandler_WithInvalidJsonResponse_ThrowsException()
        {
            // Arrange
            var headers = new Dictionary<string, string>
            {
                { "Authorization", "Bearer invalid-token" }
            };

            var request = new Dictionary<string, object>
            {
                { "headers", JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(headers)) },
                { "methodArn", "arn:aws:execute-api:us-east-1:123456789012:api-id/stage/GET/videos/123" }
            };

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _function.FunctionHandler(request, _context));
        }

        [Fact]
        public async Task FunctionHandler_WithInvalidIssuer_ThrowsException()
        {
            // Arrange
            var tokenHandler = new JwtSecurityTokenHandler();
            var now = DateTime.UtcNow;

            var claims = new[]
            {
                new Claim("sub", _validClientId),
                new Claim("exp", now.AddHours(1).ToString("o")),
                new Claim("client_id", _validClientId)
            };

            var token = new JwtSecurityToken(
                issuer: "https://invalid-issuer.com",
                audience: _validClientId,
                claims: claims,
                expires: now.AddHours(1),
                signingCredentials: _signingCredentials
            );

            var invalidToken = tokenHandler.WriteToken(token);

            var headers = new Dictionary<string, string>
            {
                { "Authorization", $"Bearer {invalidToken}" }
            };

            var request = new Dictionary<string, object>
            {
                { "headers", JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(headers)) },
                { "methodArn", "arn:aws:execute-api:us-east-1:123456789012:api-id/stage/GET/videos/123" }
            };

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _function.FunctionHandler(request, _context));
        }

        [Fact]
        public async Task FunctionHandler_WithInvalidClientId_ThrowsException()
        {
            // Arrange
            var tokenHandler = new JwtSecurityTokenHandler();
            var now = DateTime.UtcNow;

            var claims = new[]
            {
                new Claim("sub", "invalid-client-id"),
                new Claim("token_use", "access"),
                new Claim("scope", "aws.cognito.signin.user.admin"),
                new Claim("auth_time", now.ToString("o")),
                new Claim("iss", "https://cognito-idp.us-east-1.amazonaws.com/us-east-1_WkWACahpJ"),
                new Claim("exp", now.AddHours(1).ToString("o")),
                new Claim("iat", now.ToString("o")),
                new Claim("jti", Guid.NewGuid().ToString()),
                new Claim("client_id", "invalid-client-id"),
                new Claim("username", "invalid-client-id")
            };

            var token = new JwtSecurityToken(
                issuer: "https://cognito-idp.us-east-1.amazonaws.com/us-east-1_WkWACahpJ",
                audience: "invalid-client-id",
                claims: claims,
                expires: now.AddHours(1),
                signingCredentials: _signingCredentials
            );

            var invalidToken = tokenHandler.WriteToken(token);

            var headers = new Dictionary<string, string>
            {
                { "Authorization", $"Bearer {invalidToken}" }
            };

            var request = new Dictionary<string, object>
            {
                { "headers", JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(headers)) },
                { "methodArn", "arn:aws:execute-api:us-east-1:123456789012:api-id/stage/GET/videos/123" }
            };

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _function.FunctionHandler(request, _context));
        }

        [Fact]
        public async Task FunctionHandler_WithMissingRequiredClaims_ThrowsException()
        {
            // Arrange
            var tokenHandler = new JwtSecurityTokenHandler();
            var now = DateTime.UtcNow;

            var claims = new[]
            {
                new Claim("sub", _validClientId),
                new Claim("exp", now.AddHours(1).ToString("o")),
                new Claim("iss", "https://cognito-idp.us-east-1.amazonaws.com/us-east-1_WkWACahpJ")
                // Faltando claims obrigatórias como token_use, scope, etc.
            };

            var token = new JwtSecurityToken(
                issuer: "https://cognito-idp.us-east-1.amazonaws.com/us-east-1_WkWACahpJ",
                audience: _validClientId,
                claims: claims,
                expires: now.AddHours(1),
                signingCredentials: _signingCredentials
            );

            var invalidToken = tokenHandler.WriteToken(token);

            var headers = new Dictionary<string, string>
            {
                { "Authorization", $"Bearer {invalidToken}" }
            };

            var request = new Dictionary<string, object>
            {
                { "headers", JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(headers)) },
                { "methodArn", "arn:aws:execute-api:us-east-1:123456789012:api-id/stage/GET/videos/123" }
            };

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _function.FunctionHandler(request, _context));
        }

        [Fact]
        public async Task FunctionHandler_WithJwksFetchError_ThrowsException()
        {
            // Arrange
            var headers = new Dictionary<string, string>
            {
                { "Authorization", "Bearer valid.token.here" }
            };

            var request = new Dictionary<string, object>
            {
                { "headers", JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(headers)) },
                { "methodArn", "arn:aws:execute-api:us-east-1:123456789012:api-id/stage/GET/videos/123" }
            };

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _function.FunctionHandler(request, _context));
        }

        [Fact]
        public async Task FunctionHandler_WithInvalidClaims_ThrowsException()
        {
            // Arrange
            var tokenHandler = new JwtSecurityTokenHandler();
            var now = DateTime.UtcNow;

            var claims = new[]
            {
                new Claim("sub", "invalid-sub"),
                new Claim("token_use", "invalid-use"),
                new Claim("scope", "invalid-scope"),
                new Claim("auth_time", now.ToString("o")),
                new Claim("iss", "https://cognito-idp.us-east-1.amazonaws.com/us-east-1_WkWACahpJ"),
                new Claim("exp", now.AddHours(1).ToString("o")),
                new Claim("iat", now.ToString("o")),
                new Claim("jti", Guid.NewGuid().ToString()),
                new Claim("client_id", _validClientId),
                new Claim("username", _validClientId)
            };

            var token = new JwtSecurityToken(
                issuer: "https://cognito-idp.us-east-1.amazonaws.com/us-east-1_WkWACahpJ",
                audience: _validClientId,
                claims: claims,
                expires: now.AddHours(1),
                signingCredentials: _signingCredentials
            );

            var headers = new Dictionary<string, string>
            {
                { "Authorization", $"Bearer {tokenHandler.WriteToken(token)}" }
            };

            var request = new Dictionary<string, object>
            {
                { "headers", JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(headers)) },
                { "methodArn", "arn:aws:execute-api:us-east-1:123456789012:api-id/stage/GET/videos/123" }
            };

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _function.FunctionHandler(request, _context));
        }

        [Fact]
        public async Task FunctionHandler_WithInvalidClientIdInClaims_ThrowsException()
        {
            // Arrange
            var tokenHandler = new JwtSecurityTokenHandler();
            var now = DateTime.UtcNow;

            var claims = new[]
            {
                new Claim("sub", "invalid-client"),
                new Claim("token_use", "access"),
                new Claim("scope", "aws.cognito.signin.user.admin"),
                new Claim("auth_time", now.ToString("o")),
                new Claim("iss", "https://cognito-idp.us-east-1.amazonaws.com/us-east-1_WkWACahpJ"),
                new Claim("exp", now.AddHours(1).ToString("o")),
                new Claim("iat", now.ToString("o")),
                new Claim("jti", Guid.NewGuid().ToString()),
                new Claim("client_id", "invalid-client-id"),
                new Claim("username", "invalid-client")
            };

            var token = new JwtSecurityToken(
                issuer: "https://cognito-idp.us-east-1.amazonaws.com/us-east-1_WkWACahpJ",
                audience: "invalid-client-id",
                claims: claims,
                expires: now.AddHours(1),
                signingCredentials: _signingCredentials
            );

            var headers = new Dictionary<string, string>
            {
                { "Authorization", $"Bearer {tokenHandler.WriteToken(token)}" }
            };

            var request = new Dictionary<string, object>
            {
                { "headers", JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(headers)) },
                { "methodArn", "arn:aws:execute-api:us-east-1:123456789012:api-id/stage/GET/videos/123" }
            };

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _function.FunctionHandler(request, _context));
        }

        [Fact]
        public async Task FunctionHandler_WithMissingClaims_ThrowsException()
        {
            // Arrange
            var tokenHandler = new JwtSecurityTokenHandler();
            var now = DateTime.UtcNow;

            var claims = new[]
            {
                new Claim("sub", _validClientId),
                new Claim("token_use", "access"),
                new Claim("scope", "aws.cognito.signin.user.admin"),
                new Claim("auth_time", now.ToString("o")),
                new Claim("iss", "https://cognito-idp.us-east-1.amazonaws.com/us-east-1_WkWACahpJ"),
                new Claim("exp", now.AddHours(1).ToString("o")),
                new Claim("iat", now.ToString("o")),
                new Claim("jti", Guid.NewGuid().ToString())
                // Removendo client_id e username para testar o cenário de claims ausentes
            };

            var token = new JwtSecurityToken(
                issuer: "https://cognito-idp.us-east-1.amazonaws.com/us-east-1_WkWACahpJ",
                audience: _validClientId,
                claims: claims,
                expires: now.AddHours(1),
                signingCredentials: _signingCredentials
            );

            var headers = new Dictionary<string, string>
            {
                { "Authorization", $"Bearer {tokenHandler.WriteToken(token)}" }
            };

            var request = new Dictionary<string, object>
            {
                { "headers", JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(headers)) },
                { "methodArn", "arn:aws:execute-api:us-east-1:123456789012:api-id/stage/GET/videos/123" }
            };

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _function.FunctionHandler(request, _context));
        }

        [Fact]
        public async Task FunctionHandler_WithTokenValidationError_ThrowsException()
        {
            // Arrange
            var tokenHandler = new JwtSecurityTokenHandler();
            var now = DateTime.UtcNow;

            var claims = new[]
            {
                new Claim("sub", _validClientId),
                new Claim("token_use", "access"),
                new Claim("scope", "aws.cognito.signin.user.admin"),
                new Claim("auth_time", now.ToString("o")),
                new Claim("iss", "https://cognito-idp.us-east-1.amazonaws.com/us-east-1_WkWACahpJ"),
                new Claim("exp", now.AddHours(-1).ToString("o")), // Token expirado
                new Claim("iat", now.ToString("o")),
                new Claim("jti", Guid.NewGuid().ToString()),
                new Claim("client_id", _validClientId),
                new Claim("username", _validClientId)
            };

            var token = new JwtSecurityToken(
                issuer: "https://cognito-idp.us-east-1.amazonaws.com/us-east-1_WkWACahpJ",
                audience: _validClientId,
                claims: claims,
                expires: now.AddHours(-1), // Token expirado
                signingCredentials: _signingCredentials
            );

            var headers = new Dictionary<string, string>
            {
                { "Authorization", $"Bearer {tokenHandler.WriteToken(token)}" }
            };

            var request = new Dictionary<string, object>
            {
                { "headers", JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(headers)) },
                { "methodArn", "arn:aws:execute-api:us-east-1:123456789012:api-id/stage/GET/videos/123" }
            };

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _function.FunctionHandler(request, _context));
        }

        [Fact]
        public async Task FunctionHandler_WithUnexpectedError_ThrowsException()
        {
            // Arrange
            var tokenHandler = new JwtSecurityTokenHandler();
            var now = DateTime.UtcNow;

            var claims = new[]
            {
                new Claim("sub", _validClientId),
                new Claim("token_use", "access"),
                new Claim("scope", "aws.cognito.signin.user.admin"),
                new Claim("auth_time", now.ToString("o")),
                new Claim("iss", "https://cognito-idp.us-east-1.amazonaws.com/us-east-1_WkWACahpJ"),
                new Claim("exp", "invalid-date"), // Data inválida para causar erro
                new Claim("iat", now.ToString("o")),
                new Claim("jti", Guid.NewGuid().ToString()),
                new Claim("client_id", _validClientId),
                new Claim("username", _validClientId)
            };

            var token = new JwtSecurityToken(
                issuer: "https://cognito-idp.us-east-1.amazonaws.com/us-east-1_WkWACahpJ",
                audience: _validClientId,
                claims: claims,
                expires: now.AddHours(1),
                signingCredentials: _signingCredentials
            );

            var headers = new Dictionary<string, string>
            {
                { "Authorization", $"Bearer {tokenHandler.WriteToken(token)}" }
            };

            var request = new Dictionary<string, object>
            {
                { "headers", JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(headers)) },
                { "methodArn", "arn:aws:execute-api:us-east-1:123456789012:api-id/stage/GET/videos/123" }
            };

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _function.FunctionHandler(request, _context));
        }

        [Fact]
        public async Task FunctionHandler_WithInvalidTokenFormat_ThrowsException()
        {
            // Arrange
            var headers = new Dictionary<string, string>
            {
                { "Authorization", "Bearer invalid.token.format.with.extra.dots" }
            };

            var request = new Dictionary<string, object>
            {
                { "headers", JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(headers)) },
                { "methodArn", "arn:aws:execute-api:us-east-1:123456789012:api-id/stage/GET/videos/123" }
            };

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _function.FunctionHandler(request, _context));
        }

        [Fact]
        public async Task FunctionHandler_WithMalformedTokenStructure_ThrowsException()
        {
            // Arrange
            var headers = new Dictionary<string, string>
            {
                { "Authorization", "Bearer " + Convert.ToBase64String(Encoding.UTF8.GetBytes("malformed_token_structure")) }
            };

            var request = new Dictionary<string, object>
            {
                { "headers", JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(headers)) },
                { "methodArn", "arn:aws:execute-api:us-east-1:123456789012:api-id/stage/GET/videos/123" }
            };

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _function.FunctionHandler(request, _context));
        }

        [Fact]
        public async Task FunctionHandler_WithExpiredTokenValidation_ThrowsException()
        {
            // Arrange
            var tokenHandler = new JwtSecurityTokenHandler();
            var now = DateTime.UtcNow;

            var claims = new[]
            {
                new Claim("sub", _validClientId),
                new Claim("token_use", "access"),
                new Claim("scope", "aws.cognito.signin.user.admin"),
                new Claim("auth_time", now.AddDays(-1).ToString("o")),
                new Claim("iss", "https://cognito-idp.us-east-1.amazonaws.com/us-east-1_WkWACahpJ"),
                new Claim("exp", now.AddDays(-1).ToString("o")),
                new Claim("iat", now.AddDays(-1).ToString("o")),
                new Claim("jti", Guid.NewGuid().ToString()),
                new Claim("client_id", _validClientId),
                new Claim("username", _validClientId)
            };

            var token = new JwtSecurityToken(
                issuer: "https://cognito-idp.us-east-1.amazonaws.com/us-east-1_WkWACahpJ",
                audience: _validClientId,
                claims: claims,
                expires: now.AddDays(-1),
                signingCredentials: _signingCredentials
            );

            var headers = new Dictionary<string, string>
            {
                { "Authorization", $"Bearer {tokenHandler.WriteToken(token)}" }
            };

            var request = new Dictionary<string, object>
            {
                { "headers", JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(headers)) },
                { "methodArn", "arn:aws:execute-api:us-east-1:123456789012:api-id/stage/GET/videos/123" }
            };

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _function.FunctionHandler(request, _context));
        }

        [Fact]
        public async Task FunctionHandler_WithInvalidJwksResponse_ThrowsException()
        {
            // Arrange
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("invalid_jwks_response")
                });

            var mockHttpClient = new HttpClient(mockHttpMessageHandler.Object);

            var headers = new Dictionary<string, string>
            {
                { "Authorization", "Bearer " + Convert.ToBase64String(Encoding.UTF8.GetBytes("test_token")) }
            };

            var request = new Dictionary<string, object>
            {
                { "headers", JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(headers)) },
                { "methodArn", "arn:aws:execute-api:us-east-1:123456789012:api-id/stage/GET/videos/123" }
            };

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _function.FunctionHandler(request, _context));
        }

        [Fact]
        public async Task FunctionHandler_WithInvalidJwksResponseAndMalformedToken_ThrowsException()
        {
            // Arrange
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("{\"keys\":[{\"invalid\":\"jwks\"}]}")
                });

            var mockHttpClient = new HttpClient(mockHttpMessageHandler.Object);

            var headers = new Dictionary<string, string>
            {
                { "Authorization", "Bearer " + Convert.ToBase64String(Encoding.UTF8.GetBytes("malformed_token")) }
            };

            var request = new Dictionary<string, object>
            {
                { "headers", JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(headers)) },
                { "methodArn", "arn:aws:execute-api:us-east-1:123456789012:api-id/stage/GET/videos/123" }
            };

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _function.FunctionHandler(request, _context));
        }

        [Fact]
        public async Task FunctionHandler_WithInvalidJwksResponseAndExpiredToken_ThrowsException()
        {
            // Arrange
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("{\"keys\":[{\"invalid\":\"jwks\"}]}")
                });

            var mockHttpClient = new HttpClient(mockHttpMessageHandler.Object);

            var tokenHandler = new JwtSecurityTokenHandler();
            var now = DateTime.UtcNow;

            var claims = new[]
            {
                new Claim("sub", _validClientId),
                new Claim("token_use", "access"),
                new Claim("scope", "aws.cognito.signin.user.admin"),
                new Claim("auth_time", now.AddDays(-1).ToString("o")),
                new Claim("iss", "https://cognito-idp.us-east-1.amazonaws.com/us-east-1_WkWACahpJ"),
                new Claim("exp", now.AddDays(-1).ToString("o")),
                new Claim("iat", now.AddDays(-1).ToString("o")),
                new Claim("jti", Guid.NewGuid().ToString()),
                new Claim("client_id", _validClientId),
                new Claim("username", _validClientId)
            };

            var token = new JwtSecurityToken(
                issuer: "https://cognito-idp.us-east-1.amazonaws.com/us-east-1_WkWACahpJ",
                audience: _validClientId,
                claims: claims,
                expires: now.AddDays(-1),
                signingCredentials: _signingCredentials
            );

            var headers = new Dictionary<string, string>
            {
                { "Authorization", $"Bearer {tokenHandler.WriteToken(token)}" }
            };

            var request = new Dictionary<string, object>
            {
                { "headers", JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(headers)) },
                { "methodArn", "arn:aws:execute-api:us-east-1:123456789012:api-id/stage/GET/videos/123" }
            };

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _function.FunctionHandler(request, _context));
        }

        [Fact]
        public async Task FunctionHandler_WithInvalidJwksResponseAndInvalidClaims_ThrowsException()
        {
            // Arrange
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("{\"keys\":[{\"invalid\":\"jwks\"}]}")
                });

            var mockHttpClient = new HttpClient(mockHttpMessageHandler.Object);

            var tokenHandler = new JwtSecurityTokenHandler();
            var now = DateTime.UtcNow;

            var claims = new[]
            {
                new Claim("sub", "invalid-sub"),
                new Claim("token_use", "invalid-use"),
                new Claim("scope", "invalid-scope"),
                new Claim("auth_time", now.ToString("o")),
                new Claim("iss", "invalid-issuer"),
                new Claim("exp", now.AddHours(1).ToString("o")),
                new Claim("iat", now.ToString("o")),
                new Claim("jti", Guid.NewGuid().ToString()),
                new Claim("client_id", "invalid-client-id"),
                new Claim("username", "invalid-username")
            };

            var token = new JwtSecurityToken(
                issuer: "invalid-issuer",
                audience: "invalid-audience",
                claims: claims,
                expires: now.AddHours(1),
                signingCredentials: _signingCredentials
            );

            var headers = new Dictionary<string, string>
            {
                { "Authorization", $"Bearer {tokenHandler.WriteToken(token)}" }
            };

            var request = new Dictionary<string, object>
            {
                { "headers", JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(headers)) },
                { "methodArn", "arn:aws:execute-api:us-east-1:123456789012:api-id/stage/GET/videos/123" }
            };

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _function.FunctionHandler(request, _context));
        }
    }
}

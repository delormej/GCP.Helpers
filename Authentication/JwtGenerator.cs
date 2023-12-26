using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

namespace GcpHelpers.Authentication
{
    public class JwtGenerator
    {
        private readonly RsaSecurityKey _key;
        private readonly AuthenticationSettings _options;

        public JwtGenerator(AuthenticationSettings options)
        {
            _options = options;

            _key = new RsaSecurityKey(
                GetPrivateKey(options.PrivateKeyPemFile));
        }

        public string CreateUserAuthToken(ClaimsIdentity userClaims)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Audience = _options.ValidAudience,
                Issuer = _options.ValidIssuer,
                Subject = userClaims,
                Expires = DateTime.UtcNow.AddMinutes(_options.ExpiresInMinutes),
                SigningCredentials = new SigningCredentials(_key, SecurityAlgorithms.RsaSha256)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            
            return tokenHandler.WriteToken(token);
        }

        private RSA GetPrivateKey(string pemFilePath)
        {
            var pem = File.ReadAllText(pemFilePath);

            RSA rsa = RSA.Create();
            rsa.ImportFromPem(pem);

            return rsa;
        }
    }
}
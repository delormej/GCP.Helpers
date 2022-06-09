using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

namespace GCP.Helpers.Authentication
{
    public class JwtGenerator
    {
        private readonly RsaSecurityKey _key;
        private readonly string _audience;
        private readonly string _issuer;

        public JwtGenerator(AuthenticationSettings options)
        {
            _audience = options.ValidAudience;

            _issuer = options.ValidIssuer;
           
            _key = new RsaSecurityKey(
                GetPrivateKey(options.PrivateKeyPemFile));
        }

        public string CreateUserAuthToken(ClaimsIdentity userClaims)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Audience = _audience,
                Issuer = _issuer,
                Subject = userClaims,
                Expires = DateTime.UtcNow.AddMinutes(60),
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
using System;
using System.Configuration;
using System.IdentityModel.Tokens;
using Huygens.Compatibility;
using WrapperCommon.Azure;

namespace WrapperCommon.Security
{
    /// <summary>
    /// Verify security with Azure Active Directory
    /// </summary>
    public class AadSecurityCheck: ISecurityCheck {

        private static readonly string TennantKey = SecuritySettings.Config.TennantKey;
        private static readonly string Audience = SecuritySettings.Config.Audience;
        private static readonly string Issuer = SecuritySettings.Config.AadTokenIssuer + TennantKey + "/";

        /// <summary>
        /// Read authentication headers and check them against an AAD server.
        /// </summary>
        public SecurityOutcome Validate(IContext ctx)
        {
            try {
                var token = ctx.Request.Headers.Get("Authorization") ?? ctx.Request.Headers.Get("WWW-Authenticate");
                if (string.IsNullOrWhiteSpace(token)) return SecurityOutcome.Fail;
                token = token.Replace("Bearer ", "");
                if (string.IsNullOrWhiteSpace(token)) return SecurityOutcome.Fail;

                // Set-up the validator...
                using (var signingTokens = SigningKeys.AllAvailableKeys())
                {
                    var validationParams = new TokenValidationParameters
                    {
                        ValidAudience = Audience,
                        ValidIssuer = Issuer,
                        IssuerSigningTokens = signingTokens
                    };
                    var x = new JwtSecurityTokenHandler();
                    x.ValidateToken(token, validationParams, out var y);

                    return (y == null) ? SecurityOutcome.Fail : SecurityOutcome.Pass;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return SecurityOutcome.Fail;
            }
        }
    }
}
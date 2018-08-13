using Huygens.Compatibility;

namespace WrapperRoleListener.Internal.Security
{
    /// <summary>
    /// A security check for running is fully hosted environments.
    /// This will always pass the validity check without checking anything.
    /// </summary>
    public class AcceptInsecure : ISecurityCheck
    {
        public SecurityOutcome Validate(IContext context)
        {
            return SecurityOutcome.Pass;
        }
    }
}
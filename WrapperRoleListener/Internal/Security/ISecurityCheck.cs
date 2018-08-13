using Huygens.Compatibility;

namespace WrapperRoleListener.Internal.Security
{
    public interface ISecurityCheck
    {
        SecurityOutcome Validate(IContext context);
    }
}
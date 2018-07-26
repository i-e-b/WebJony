using Huygens.Compatibility;

namespace WrapperCommon.Security
{
    public interface ISecurityCheck
    {
        SecurityOutcome Validate(IContext context);
    }
}
using System;

namespace WrapperMarkerAttributes
{
    /// <summary>
    /// This marker should be put on at least one start-up type for your web API.
    /// Each class marked with this attribute should have at least one method marked with the `ApplicationSetupMethodAttribute`.
    /// All classes marked with this attribute must have a public parameterless constructor.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ApplicationSetupPointAttribute : Attribute
    {
        /// <summary>
        /// The major revision number of this site
        /// </summary>
        public int ApiMajorVersion { get; }

        protected ApplicationSetupPointAttribute() { }

        /// <summary>
        /// Mark a class as a hostable API with a versioned API. Major version must be greater than zero.
        /// </summary>
        /// <param name="ApiMajorVersion">The major version of the API. This should be incremented on any incompatible changes</param>
        public ApplicationSetupPointAttribute(int ApiMajorVersion)
        {
            this.ApiMajorVersion = ApiMajorVersion;
        }
    }
}
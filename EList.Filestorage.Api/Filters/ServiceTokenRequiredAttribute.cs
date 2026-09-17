using EList.Filestorage.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace EList.Filestorage.Api.Filters
{
    /// <summary>
    /// Endpoint available only with elist.api service-token (not end-user JWT session).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class ServiceTokenRequiredAttribute : Attribute, IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var storage = context.HttpContext.RequestServices.GetService(typeof(IAuthorizationDataStorage))
                as IAuthorizationDataStorage;

            if (storage == null || !storage.IsServiceRequest)
            {
                context.Result = new UnauthorizedObjectResult(new
                {
                    Success = false,
                    ErrorCode = 1,
                    Message = "Требуется service-token (internal API)"
                });
            }
        }
    }
}

using Microsoft.AspNet.OData.Adapters;
using Microsoft.AspNet.OData.Extensions;
using Microsoft.AspNet.OData.Routing.Conventions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;

namespace Microsoft.AspNetCore.OData.Routing.Conventions
{
    /// <summary>
    /// 
    /// </summary>
    public class DynamicControllerRoutingConvention : IODataRoutingConvention
    {
        /// <inheritdoc/>
        /// <remarks>This signature uses types that are AspNetCore-specific.</remarks>
        public IEnumerable<ControllerActionDescriptor> SelectAction(RouteContext routeContext)
        {
            if (routeContext == null)
            {
                throw new ArgumentNullException("routeContext");
            }

            Microsoft.AspNet.OData.Routing.ODataPath odataPath = routeContext.HttpContext.ODataFeature().Path;
            if (odataPath == null)
            {
                throw new ArgumentNullException("odataPath");
            }

            HttpRequest request = routeContext.HttpContext.Request;

            SelectControllerResult controllerResult = SelectControllerImpl(odataPath);
            if (controllerResult != null)
            {
                // Get a IActionDescriptorCollectionProvider from the global service provider.
                IActionDescriptorCollectionProvider actionCollectionProvider =
                    routeContext.HttpContext.RequestServices.GetRequiredService<IActionDescriptorCollectionProvider>();
                Contract.Assert(actionCollectionProvider != null);

                IEnumerable<ControllerActionDescriptor> actionDescriptors = actionCollectionProvider
                    .ActionDescriptors.Items.OfType<ControllerActionDescriptor>()
                    .Where(c => c.AttributeRouteInfo != null && c.AttributeRouteInfo.Template == controllerResult.ControllerName);

                if (actionDescriptors != null)
                {
                    string actionName = SelectAction(routeContext, controllerResult, actionDescriptors);
                    if (!String.IsNullOrEmpty(actionName))
                    {
                        return actionDescriptors.Where(
                            c => String.Equals(c.ActionName, actionName, StringComparison.OrdinalIgnoreCase));
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Selects the controller for OData requests.
        /// </summary>
        /// <param name="odataPath">The OData path.</param>
        /// <returns>
        ///   <c>null</c> if the request isn't handled by this convention; otherwise, the name of the selected controller
        /// </returns>
        internal static SelectControllerResult SelectControllerImpl(Microsoft.AspNet.OData.Routing.ODataPath odataPath)
        {
            // entity set
            EntitySetSegment entitySetSegment = odataPath.Segments.FirstOrDefault() as EntitySetSegment;
            if (entitySetSegment != null)
            {
                return new SelectControllerResult(entitySetSegment.EntitySet.Name, null);
            }

            // singleton
            SingletonSegment singletonSegment = odataPath.Segments.FirstOrDefault() as SingletonSegment;
            if (singletonSegment != null)
            {
                return new SelectControllerResult(singletonSegment.Singleton.Name, null);
            }

            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="routeContext"></param>
        /// <param name="controllerResult"></param>
        /// <param name="actionDescriptors"></param>
        /// <returns></returns>
        public string SelectAction(RouteContext routeContext, SelectControllerResult controllerResult, IEnumerable<ControllerActionDescriptor> actionDescriptors)
        {
            return SelectActionImpl(routeContext, controllerResult, actionDescriptors);
        }

        private string SelectActionImpl(RouteContext routeContext, SelectControllerResult controllerResult, IEnumerable<ControllerActionDescriptor> actionDescriptors)
        {
            Microsoft.AspNet.OData.Routing.ODataPath odataPath = routeContext.HttpContext.ODataFeature().Path;
            HttpRequest request = routeContext.HttpContext.Request;

            if (odataPath.PathTemplate == "~/entityset")
            {
                EntitySetSegment entitySetSegment = (EntitySetSegment)odataPath.Segments[0];
                IEdmEntitySetBase entitySet = entitySetSegment.EntitySet;

                if (HttpMethods.IsGet(request.Method))
                {
                    // e.g. Try GetCustomers first, then fall back to Get action name
                    return actionDescriptors.FindMatchingAction(
                        "Get" + entitySet.Name,
                        "Get");
                }
                else if (HttpMethods.IsPost(request.Method))
                {
                    // e.g. Try PostCustomer first, then fall back to Post action name
                    return actionDescriptors.FindMatchingAction(
                        "Post" + entitySet.EntityType().Name,
                        "Post");
                }
            }
            else if (odataPath.PathTemplate == "~/entityset/key" ||
                odataPath.PathTemplate == "~/entityset/key/cast")
            {
                string httpMethodName;

                if (HttpMethods.IsGet(request.Method))
                    httpMethodName = "Get";
                else if (HttpMethods.IsPut(request.Method))
                    httpMethodName = "Put";
                else if (HttpMethods.IsPatch(request.Method))
                    httpMethodName = "Patch";
                else if (HttpMethods.IsDelete(request.Method))
                    httpMethodName = "Delete";
                else
                    return null;

                Contract.Assert(httpMethodName != null);

                IEdmEntityType entityType = (IEdmEntityType)odataPath.EdmType;

                // e.g. Try GetCustomer first, then fallback on Get action name
                string actionName = actionDescriptors.FindMatchingAction(
                    httpMethodName + entityType.Name,
                    httpMethodName);

                if (actionName != null)
                {
                    KeySegment keySegment = (KeySegment)odataPath.Segments[1];
                    var controllerContext = new WebApiControllerContext(routeContext, controllerResult);
                    controllerContext.AddKeyValueToRouteData(keySegment);
                    return actionName;
                }
            }
            else if (odataPath.PathTemplate == "~/entityset/$count" &&
                HttpMethods.IsGet(request.Method))
            {
                EntitySetSegment entitySetSegment = (EntitySetSegment)odataPath.Segments[0];
                IEdmEntitySetBase entitySet = entitySetSegment.EntitySet;

                // e.g. Try GetCustomers first, then fall back to Get action name
                return actionDescriptors.FindMatchingAction(
                    "Get" + entitySet.Name,
                    "Get");
            }
            else if (odataPath.PathTemplate == "~/entityset/cast")
            {
                EntitySetSegment entitySetSegment = (EntitySetSegment)odataPath.Segments[0];
                IEdmEntitySetBase entitySet = entitySetSegment.EntitySet;
                IEdmCollectionType collectionType = (IEdmCollectionType)odataPath.EdmType;
                IEdmEntityType entityType = (IEdmEntityType)collectionType.ElementType.Definition;

                if (HttpMethods.IsGet(request.Method))
                {
                    // e.g. Try GetCustomersFromSpecialCustomer first, then fall back to GetFromSpecialCustomer
                    return actionDescriptors.FindMatchingAction(
                        "Get" + entitySet.Name + "From" + entityType.Name,
                        "GetFrom" + entityType.Name);
                }
                else if (HttpMethods.IsPost(request.Method))
                {
                    // e.g. Try PostCustomerFromSpecialCustomer first, then fall back to PostFromSpecialCustomer
                    return actionDescriptors.FindMatchingAction(
                        "Post" + entitySet.EntityType().Name + "From" + entityType.Name,
                        "PostFrom" + entityType.Name);
                }
            }
            else if (odataPath.PathTemplate == "~/entityset/cast/$count" &&
                HttpMethods.IsGet(request.Method))
            {
                EntitySetSegment entitySetSegment = (EntitySetSegment)odataPath.Segments[0];
                IEdmEntitySetBase entitySet = entitySetSegment.EntitySet;
                IEdmCollectionType collectionType = (IEdmCollectionType)odataPath.Segments[1].EdmType;
                IEdmEntityType entityType = (IEdmEntityType)collectionType.ElementType.Definition;

                // e.g. Try GetCustomersFromSpecialCustomer first, then fall back to GetFromSpecialCustomer
                return actionDescriptors.FindMatchingAction(
                    "Get" + entitySet.Name + "From" + entityType.Name,
                    "GetFrom" + entityType.Name);
            }

            return null;
        }
    }

    internal static class WebApiActionExtensions
    {
        internal static bool Contains(this IEnumerable<ControllerActionDescriptor> actionDescriptors, string name)
        {
            return actionDescriptors.Any(a => String.Equals(a.ActionName, name, StringComparison.InvariantCultureIgnoreCase));
        }

        public static string FindMatchingAction(this IEnumerable<ControllerActionDescriptor> actionDescriptors, params string[] targetActionNames)
        {
            foreach (string targetActionName in targetActionNames)
            {
                if (actionDescriptors.Contains(targetActionName))
                {
                    return targetActionName;
                }
            }

            return null;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Autofac;
using Autofac.Core;
using Autofac.Core.Lifetime;
using Autofac.Core.Resolving;
using CodEaisy.TinySaas.Interface;

namespace CodEaisy.TinySaas.Core
{
    internal class MultiTenantContainer<T> : IContainer where T : ITenant
    {
        //This is the base application container
        private readonly IContainer _applicationContainer;
        //This action configures a container builder
        private readonly Action<ITenantContext<T>, ContainerBuilder> _tenantContainerConfiguration;

        //This dictionary keeps track of all of the tenant scopes that we have created
        private readonly Dictionary<string, ILifetimeScope> _tenantLifetimeScopes = new Dictionary<string, ILifetimeScope>();

        private readonly object _lock = new object();
        private const string _multiTenantTag = "multitenantcontainer";

        public IDisposer Disposer => _applicationContainer.Disposer;

        public object Tag => _applicationContainer.Tag;

        public IComponentRegistry ComponentRegistry => _applicationContainer.ComponentRegistry;

        public MultiTenantContainer(IContainer applicationContainer, Action<ITenantContext<T>, ContainerBuilder> containerConfiguration)
        {
            _tenantContainerConfiguration = containerConfiguration;
            _applicationContainer = applicationContainer;
        }

        public event EventHandler<LifetimeScopeBeginningEventArgs> ChildLifetimeScopeBeginning
        {
            add
            {
                _applicationContainer.ChildLifetimeScopeBeginning += value;
            }

            remove
            {
                _applicationContainer.ChildLifetimeScopeBeginning -= value;
            }
        }

        public event EventHandler<LifetimeScopeEndingEventArgs> CurrentScopeEnding
        {
            add
            {
                _applicationContainer.CurrentScopeEnding += value;
            }

            remove
            {
                _applicationContainer.CurrentScopeEnding -= value;
            }
        }

        public event EventHandler<ResolveOperationBeginningEventArgs> ResolveOperationBeginning
        {
            add
            {
                _applicationContainer.ResolveOperationBeginning += value;
            }

            remove
            {
                _applicationContainer.ResolveOperationBeginning -= value;
            }
        }

        /// <summary>
        /// Get the current teanant from the application container
        /// </summary>
        /// <returns></returns>
        private ITenantContext<T> GetCurrentTenant()
        {
            //We have registered our TenantAccessService in Part 1, the service is available in the application container which allows us to access the current Tenant
            return  _applicationContainer.Resolve<ITenantContextService<T>>().GetTenantContext().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Get the scope of the current tenant
        /// </summary>
        /// <returns></returns>
        public ILifetimeScope GetCurrentTenantScope()
        {
            return GetTenantScope(GetCurrentTenant()?.Tenant?.Id.ToString());
        }

        /// <summary>
        /// Get (configure on missing)
        /// </summary>
        /// <param name="tenantId"></param>
        /// <returns></returns>
        public ILifetimeScope GetTenantScope(string tenantId)
        {
            //If no tenant (e.g. early on in the pipeline, we just use the application container)
            if (tenantId == null)
                return _applicationContainer;

            //If we have created a lifetime for a tenant, return
            if (_tenantLifetimeScopes.ContainsKey(tenantId))
                return _tenantLifetimeScopes[tenantId];

            lock (_lock)
            {
                if (_tenantLifetimeScopes.ContainsKey(tenantId))
                {
                    return _tenantLifetimeScopes[tenantId];
                }
                else
                {
                    //This is a new tenant, configure a new lifetimescope for it using our tenant sensitive configuration method
                    _tenantLifetimeScopes.Add(tenantId, _applicationContainer.BeginLifetimeScope(_multiTenantTag, a => _tenantContainerConfiguration(GetCurrentTenant(), a)));
                    return _tenantLifetimeScopes[tenantId];
                }
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                foreach (var scope in _tenantLifetimeScopes)
                    scope.Value.Dispose();
                _applicationContainer.Dispose();
            }
        }

        public ILifetimeScope BeginLifetimeScope()
        {
            return _applicationContainer.BeginLifetimeScope();
        }

        public ILifetimeScope BeginLifetimeScope(object tag)
        {
            return _applicationContainer.BeginLifetimeScope(tag);
        }

        public ILifetimeScope BeginLifetimeScope(Action<ContainerBuilder> configurationAction)
        {
            return _applicationContainer.BeginLifetimeScope(configurationAction);
        }

        public ILifetimeScope BeginLifetimeScope(object tag, Action<ContainerBuilder> configurationAction)
        {
            return _applicationContainer.BeginLifetimeScope(tag, configurationAction);
        }

        public object ResolveComponent(ResolveRequest request)
        {
            return _applicationContainer.ResolveComponent(request);
        }

        public ValueTask DisposeAsync()
        {
            return _applicationContainer.DisposeAsync();
        }
    }
}

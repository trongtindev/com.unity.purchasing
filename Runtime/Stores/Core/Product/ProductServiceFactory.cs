#nullable enable
using System;
using System.Collections.Generic;
using Uniject;
using UnityEngine.Purchasing.Extension;
using UnityEngine.Purchasing.Interfaces;
using UnityEngine.Purchasing.Services;
using UnityEngine.Purchasing.Telemetry;
using UnityEngine.Purchasing.UseCases;
using UnityEngine.Purchasing.Utilities;

namespace UnityEngine.Purchasing
{
    public class ProductServiceFactory : IProductServiceFactory
    {
        static ProductServiceFactory? s_Instance;

        readonly Dictionary<string?, Func<IStoreWrapper, IProductService>> m_ProductServiceInstantiationByName = new Dictionary<string?, Func<IStoreWrapper, IProductService>>();

        internal static ProductServiceFactory Instance()
        {
            return s_Instance ??= new ProductServiceFactory();
        }

        ProductServiceFactory()
        {
            m_ProductServiceInstantiationByName.Add(AppleAppStore.Name, CreateAppleProductService);
            m_ProductServiceInstantiationByName.Add(MacAppStore.Name, CreateAppleProductService);
            m_ProductServiceInstantiationByName.Add(AmazonApps.Name, CreateAmazonProductService);
        }

        public void RegisterNewService(string name, Func<IProductService> createFunction)
        {
            IProductService CreateCustomProductServiceDiscardParams(IStoreWrapper storeWrapper)
            {
                return createFunction?.Invoke() ?? throw new InvalidOperationException();
            }

            m_ProductServiceInstantiationByName.Add(name, CreateCustomProductServiceDiscardParams);
        }

        public void RegisterNewExtendedService(string name, Func<IProductService, ExtensibleProductService> createFunction)
        {
            IProductService CreateCustomProductServiceDiscardParams(IStoreWrapper storeWrapper)
            {
                return createFunction?.Invoke(CreateGenericProductService(storeWrapper)) ?? throw new InvalidOperationException();
            }

            m_ProductServiceInstantiationByName.Add(name, CreateCustomProductServiceDiscardParams);
        }


        public IProductService Create(IStoreWrapper store)
        {
            return ((m_ProductServiceInstantiationByName.ContainsKey(store.name)) ? m_ProductServiceInstantiationByName[store.name]?.Invoke(store) : CreateGenericProductService(store)) ?? throw new InvalidOperationException();
        }

        static ProductService CreateGenericProductService(IStoreWrapper storeWrapper)
        {
            IDependencyInjectionService di = new DependencyInjectionService();
            AddProductServiceDependencies(storeWrapper, di);
            return di.GetInstance<ProductService>();
        }

        static void AddProductServiceDependencies(IStoreWrapper store, IDependencyInjectionService di)
        {
            di.AddInstance(new RetryService(UnityUtilContainer.Instance()));
            di.AddInstance(store.instance);
            di.AddInstance(store);
            di.AddService<FetchProductsUseCase>();
            di.AddService<ProductService>();
        }

        static AppleStoreExtendedProductService CreateAppleProductService(IStoreWrapper store)
        {

            var appleNativeStore = (store.instance as AppleStoreImpl)?.GetNativeStore();
            if (appleNativeStore == null)
            {
                throw new Exception("AppleStoreImpl's INativeAppleStore has not been set");
            }

            IDependencyInjectionService di = new DependencyInjectionService();

            di.AddInstance(appleNativeStore);
            di.AddInstance(store);
            di.AddService<ContinuePromotionalPurchasesUseCase>();
            di.AddService<FetchStorePromotionOrderUseCase>();
            di.AddService<FetchStorePromotionVisibilityUseCase>();
            di.AddInstance(StoreFactory.Instance().TelemetryDiagnosticsInstanceWrapper);
            di.AddService<TelemetryDiagnostics>();
            di.AddService<AppleReceiptConverter>();
            di.AddService<AppleRetrieveProductsService>();
            di.AddService<GetIntroductoryPriceDictionaryUseCase>();
            di.AddService<GetProductDetailsUseCase>();
            di.AddService<SetStorePromotionOrderUseCase>();
            di.AddService<SetStorePromotionVisibilityUseCase>();

            AddProductServiceDependencies(store, di);
            di.AddService<AppleStoreExtendedProductService>();

            return di.GetInstance<AppleStoreExtendedProductService>();
        }

        static AmazonAppsExtendedProductService CreateAmazonProductService(IStoreWrapper store)
        {
            IDependencyInjectionService di = new DependencyInjectionService();

            AddProductServiceDependencies(store, di);
            di.AddService<AmazonStoreCartValidator>();
            di.AddInstance(Debug.unityLogger);
            di.AddInstance(AmazonApps.Name);
            di.AddInstance(StoreFactory.Instance().TelemetryMetricsInstanceWrapper);
            di.AddService<MetricizedJsonStore>();

            IUnityCallback callback = di.GetInstance<MetricizedJsonStore>();
            var util = di.GetInstance<IUtil>();
            var amazonJavaStore = new AmazonNativeStoreBuilder().GetAmazonStore(callback, util);
            di.AddInstance(callback);
            di.AddInstance(amazonJavaStore);

            di.AddService<AmazonAppsExtendedProductService>();

            return di.GetInstance<AmazonAppsExtendedProductService>();
        }
    }
}
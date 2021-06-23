#nullable enable
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


namespace TSKT
{
    public abstract class AssetBundleLoader
    {
        static int processCount;
        readonly static List<int> waitingProcessPriorities = new List<int>();

        protected abstract UniTask<LoadResult<AssetBundle?>> Load(string filePath, uint crc = 0);

        public async UniTask<LoadResult<AssetBundle?>> LoadAssetBundle(
            string filePath,
            string assetBundleName,
            int priority,
            uint crc = 0)
        {
            {
                var assetBundle = AssetBundle.GetAllLoadedAssetBundles().FirstOrDefault(_ => _.name == assetBundleName);
                if (assetBundle)
                {
                    return new LoadResult<AssetBundle?>(assetBundle);
                }
            }

            if (processCount > 0)
            {
                waitingProcessPriorities.Add(priority);
                waitingProcessPriorities.Sort();
                await UniTask.WaitWhile(() => processCount > 0 || waitingProcessPriorities[waitingProcessPriorities.Count - 1] > priority);
                waitingProcessPriorities.Remove(priority);
                return await LoadAssetBundle(filePath: filePath, assetBundleName: assetBundleName, priority: priority, crc: crc);
            }

            ++processCount;
            try
            {
                return await Load(filePath, crc);
            }
            finally
            {
                --processCount;
            }
        }

        public async UniTask<LoadResult<T?>> LoadAsync<T>(string filePath, string assetbundleName, string assetName, int priority, uint crc = 0)
            where T : Object
        {
            var assetBundle = await LoadAssetBundle(filePath, assetbundleName, priority, crc: crc);
            if (!assetBundle.Succeeded)
            {
                return new LoadResult<T?>(default, assetBundle.state, assetBundle.exception);
            }
            var assetBundleRequest = assetBundle.value!.LoadAssetAsync<T>(assetName);
            LoadingProgress.Instance.Add(assetBundleRequest);
            await assetBundleRequest;

            var result = assetBundleRequest.asset as T;
            if (result)
            {
                return new LoadResult<T?>(result);
            }
            else
            {
                return LoadResult<T?>.CreateFailedDeserialize();
            }
        }

         public async UniTask<LoadResult<T[]?>> LoadAllAsync<T>(string filePath, string assetbundleName, int priority)
            where T : Object
        {
            var assetBundle = await LoadAssetBundle(filePath, assetbundleName, priority);
            if (!assetBundle.Succeeded)
            {
                return new LoadResult<T[]?>(default, assetBundle.state, assetBundle.exception);
            }
            var assetBundleRequest = assetBundle.value!.LoadAllAssetsAsync<T>();
            LoadingProgress.Instance.Add(assetBundleRequest);
            await assetBundleRequest;
            var result = assetBundleRequest.allAssets.OfType<T>().ToArray();
            return new LoadResult<T[]?>(result);
        }
    }
}

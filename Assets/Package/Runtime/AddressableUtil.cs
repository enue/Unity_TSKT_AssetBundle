#if TSKT_ASSETBUNDLE_SUPPORT_ADDRESSABLE
#nullable enable
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Cysharp.Threading.Tasks;

namespace TSKT
{
    public static class AddressableUtil
    {
        public static async UniTask<T?> LoadAsync<T>(object key)
            where T : Object
        {
            var request = Addressables.LoadAssetAsync<T>(key);
            var progress = LoadingProgress.Instance.Add();
            return await request.ToUniTask(progress);
        }

        public static async UniTask<T?> LoadAsync<T>(object key, GameObject owner)
            where T : Object
        {
            var request = Addressables.LoadAssetAsync<T>(key);
            Add(request, owner);
            var progress = LoadingProgress.Instance.Add();
            return await request.ToUniTask(progress);
        }

        public static async UniTask<T?> LoadAsync<T>(AssetReferenceT<T> key)
            where T : Object
        {
            var request = key.LoadAssetAsync();
            var progress = LoadingProgress.Instance.Add();
            return await request.ToUniTask(progress);
        }

        public static async UniTask<T?> LoadAsync<T>(AssetReferenceT<T> key, GameObject owner)
            where T : Object
        {
            var request = key.LoadAssetAsync();
            Add(request, owner);
            var progress = LoadingProgress.Instance.Add();
            return await request.ToUniTask(progress);
        }

        public static void Add(UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle toRelease, GameObject to)
        {
            to.GetCancellationTokenOnDestroy().Register(() =>
            {
                Addressables.Release(toRelease);
            });
        }
    }
}
#endif

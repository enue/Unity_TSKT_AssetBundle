#nullable enable
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Cysharp.Threading.Tasks;

namespace TSKT
{
    public class WebAssetBundleLoader : AssetBundleLoader
    {
        readonly CachedAssetBundle? cachedAssetBundle;

        public WebAssetBundleLoader(CachedAssetBundle? cachedAssetBundle)
        {
            this.cachedAssetBundle = cachedAssetBundle;
        }

        override protected async UniTask<LoadResult<AssetBundle?>> Load(string filePath, uint crc = 0)
        {
            using var request = cachedAssetBundle.HasValue
                ? UnityEngine.Networking.UnityWebRequestAssetBundle.GetAssetBundle(filePath, cachedAssetBundle.Value, crc)
                : UnityEngine.Networking.UnityWebRequestAssetBundle.GetAssetBundle(filePath, crc);

            var operation = request.SendWebRequest();
            LoadingProgress.Instance.Add(operation);
            await operation;
            if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                return LoadResult<AssetBundle?>.CreateError();
            }

            var result = UnityEngine.Networking.DownloadHandlerAssetBundle.GetContent(request);
            if (!result)
            {
                return LoadResult<AssetBundle?>.CreateError();
            }
            return new LoadResult<AssetBundle?>(result);
        }
    }

    public class LocalAssetBundleLoader : AssetBundleLoader
    {
        override protected async UniTask<LoadResult<AssetBundle?>> Load(string filePath, uint crc = 0)
        {
            var createRequest = AssetBundle.LoadFromFileAsync(filePath, crc);
            LoadingProgress.Instance.Add(createRequest);

            await createRequest;
            var result = createRequest.assetBundle;
            if (!result)
            {
                return LoadResult<AssetBundle?>.CreateError();
            }
            return new LoadResult<AssetBundle?>(result);
        }
    }

    public class CryptedAssetBundleLoader : AssetBundleLoader
    {
        public bool Web { get; set; }

        readonly string key;
        readonly byte[] salt;
        readonly int iteration;

        public CryptedAssetBundleLoader(string key, byte[] salt, int iteration)
        {
            this.key = key;
            this.salt = salt;
            this.iteration = iteration;
        }

        override protected async UniTask<LoadResult<AssetBundle?>> Load(string filePath, uint crc = 0)
        {
            byte[] encryptedBytes;

            var webRequest = Web;
#if WEBGL && !UNITY_EDITOR
            webRequest = true;
#endif
            if (webRequest)
            {
                using var request = UnityEngine.Networking.UnityWebRequest.Get(filePath);
                var operation = request.SendWebRequest();
                LoadingProgress.Instance.Add(operation);
                await operation;

                if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    return LoadResult<AssetBundle?>.CreateError();
                }

                encryptedBytes = request.downloadHandler.data;
            }
            else
            {
                try
                {
                    using var file = System.IO.File.OpenRead(filePath);
                    encryptedBytes = new byte[file.Length];
                    await file.ReadAsync(encryptedBytes, 0, encryptedBytes.Length).AsUniTask();
                }
                catch (System.IO.DirectoryNotFoundException ex)
                {
                    return LoadResult<AssetBundle?>.CreateNotFound(ex);
                }
                catch (System.IO.FileNotFoundException ex)
                {
                    return LoadResult<AssetBundle?>.CreateNotFound(ex);
                }
            }

            try
            {
#if UNITY_WEBGL
                var bytes = CryptUtil.Decrypt(encryptedBytes, key, salt, iteration);
#else
                var bytes = await UniTask.Run(() => CryptUtil.Decrypt(encryptedBytes, key, salt, iteration));
#endif
                var request = AssetBundle.LoadFromMemoryAsync(bytes, crc);
                LoadingProgress.Instance.Add(request);
                await request;

                return new LoadResult<AssetBundle?>(request.assetBundle);
            }
            catch (System.Exception ex)
            {
                return LoadResult<AssetBundle?>.CreateFailedDeserialize(ex);
            }
        }
    }
}

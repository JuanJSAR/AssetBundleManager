using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
#if UNITY_ANDROID
using Google.Play.AssetDelivery;
#elif UNITY_IOS
using UnityEngine.iOS;
#endif

namespace AssetBundles
{
    public struct AssetBundleDownloadCommand
    {
        public string BundleName;
        public Hash128 Hash;
        public uint Version;
        public Action<AssetBundle> OnComplete;
		public bool AssetDeliveryEnabled;
    }

    public class AssetBundleDownloader : ICommandHandler<AssetBundleDownloadCommand>
    {
        private const int MAX_RETRY_COUNT = 3;
        private const float RETRY_WAIT_PERIOD = 1;
        private const int MAX_SIMULTANEOUS_DOWNLOADS = 4;

        private static readonly Hash128 DEFAULT_HASH = default(Hash128);

        private static readonly long[] RETRY_ON_ERRORS = {
            503 // Temporary Server Error
        };

        private string baseUri;
        private Action<IEnumerator> coroutineHandler;

        private int activeDownloads = 0;
        private Queue<IEnumerator> downloadQueue = new Queue<IEnumerator>();
        private bool cachingDisabled;

        /// <summary>
        ///     Creates a new instance of the AssetBundleDownloader.
        /// </summary>
        /// <param name="baseUri">Uri to use as the base for all bundle requests.</param>
        public AssetBundleDownloader(string baseUri)
        {
            this.baseUri = baseUri;

#if UNITY_EDITOR
            if (!Application.isPlaying)
                coroutineHandler = EditorCoroutine.Start;
            else
#endif
                coroutineHandler = AssetBundleDownloaderMonobehaviour.Instance.HandleCoroutine;

            if (!this.baseUri.EndsWith("/")) {
                this.baseUri += "/";
            }
        }

        /// <summary>
        ///     Begin handling of a AssetBundleDownloadCommand object.
        /// </summary>
        public void Handle(AssetBundleDownloadCommand cmd)
        {
            InternalHandle(Download(cmd, 0));
        }

        private void InternalHandle(IEnumerator downloadCoroutine)
        {
            if (activeDownloads < MAX_SIMULTANEOUS_DOWNLOADS) {
                activeDownloads++;
                coroutineHandler(downloadCoroutine);
            } else {
                downloadQueue.Enqueue(downloadCoroutine);
            }
        }

#if UNITY_ANDROID
		private IEnumerator PlayAssetDeliveryLoadAssetBundleCoroutine(string assetBundleName, Action<AssetBundle> callback)
		{
			PlayAssetBundleRequest bundleRequest = PlayAssetDelivery.RetrieveAssetBundleAsync(assetBundleName);

			while (!bundleRequest.IsDone)
			{
				if (bundleRequest.Status == AssetDeliveryStatus.WaitingForWifi)
				{
					var userConfirmationOperation = PlayAssetDelivery.ShowCellularDataConfirmation();

					// Wait for confirmation dialog action.
					yield return userConfirmationOperation;

					if ((userConfirmationOperation.Error != AssetDeliveryErrorCode.NoError) ||
					   (userConfirmationOperation.GetResult() != ConfirmationDialogResult.Accepted))
					{
						// The user did not accept the confirmation - handle as needed.
					}

					// Wait for Wi-Fi connection OR confirmation dialog acceptance before moving on.
					yield return new WaitUntil(() => bundleRequest.Status != AssetDeliveryStatus.WaitingForWifi);
				}

				// Use bundleRequest.DownloadProgress to track download progress.
				// Use bundleRequest.Status to track the status of request.

				yield return null;
			}

			if (bundleRequest.Error != AssetDeliveryErrorCode.NoError)
			{
				// There was an error retrieving the bundle. For error codes NetworkError
				// and InsufficientStorage, you may prompt the user to check their
				// connection settings or check their storage space, respectively, then
				// try again.
				yield return null;
			}

			// Request was successful. Retrieve AssetBundle from request.AssetBundle.
			callback?.Invoke(bundleRequest.AssetBundle);
		}

#elif UNITY_IOS && ENABLE_IOS_ON_DEMAND_RESOURCES

		private IEnumerator PlayAssetDeliveryLoadAssetBundleCoroutine(string assetBundleName, Action<AssetBundle> callback)
		{
			// Create the request
			OnDemandResourcesRequest request = OnDemandResources.PreloadAsync(new string[] { assetBundleName });

			// Wait until request is completed
			yield return request;

			AssetBundle bundle = null;

			// Check for errors
			if (string.IsNullOrEmpty(request.error))
			{
				bundle = AssetBundle.LoadFromFile("res://" + assetBundleName);
			}
			else
			{
				Debug.LogError("[AssetBundleDownloader] ODR request failed: " + request.error);
			}

			callback?.Invoke(bundle);

			request.Dispose();
		}
#endif

		private IEnumerator Download(AssetBundleDownloadCommand cmd, int retryCount)
        {
			AssetBundle bundle = null;
			UnityWebRequest req = null;
			var uri = baseUri + cmd.BundleName;

#if UNITY_ANDROID || (UNITY_IOS && ENABLE_IOS_ON_DEMAND_RESOURCES)

			bool cached = !cachingDisabled && (cmd.Hash != DEFAULT_HASH ? Caching.IsVersionCached(uri, cmd.Hash) : cmd.Version > 0 && Caching.IsVersionCached(uri, new Hash128(0, 0, 0, cmd.Version)));

			if (!cached && cmd.AssetDeliveryEnabled)
			{
				if (UnityEngine.Application.platform == RuntimePlatform.Android || UnityEngine.Application.platform == RuntimePlatform.IPhonePlayer)
				{
					Debug.Log("[AssetBundleDownloader] Download - Try loading from play store asset delivery");

					yield return PlayAssetDeliveryLoadAssetBundleCoroutine(cmd.BundleName, (b) => { bundle = b; });

					Debug.Log($"[AssetBundleDownloader] Download - Asset delivery loading finished. Result = {(bundle != null)}");
				}
			}
#endif

			if (bundle == null)
			{
				if (cachingDisabled || (cmd.Version <= 0 && cmd.Hash == DEFAULT_HASH))
				{
					if (AssetBundleManager.debugLoggingEnabled)
						Debug.Log(string.Format("GetAssetBundle [{0}].", uri));

#if UNITY_2018_1_OR_NEWER
					req = UnityWebRequestAssetBundle.GetAssetBundle(uri);
#else
					req = UnityWebRequest.GetAssetBundle(uri);
#endif
				}
				else if (cmd.Hash == DEFAULT_HASH)
				{
					if (AssetBundleManager.debugLoggingEnabled)
						Debug.Log(string.Format("GetAssetBundle [{0}] v[{1}] [{2}].", Caching.IsVersionCached(uri, new Hash128(0, 0, 0, cmd.Version)) ? "cached" : "uncached", cmd.Version, uri));

#if UNITY_2018_1_OR_NEWER
					req = UnityWebRequestAssetBundle.GetAssetBundle(uri, cmd.Version, 0);
#else
					req = UnityWebRequest.GetAssetBundle(uri, cmd.Version, 0);
#endif
				}
				else
				{
					if (AssetBundleManager.debugLoggingEnabled)
						Debug.Log(string.Format("GetAssetBundle [{0}] [{1}] [{2}].", Caching.IsVersionCached(uri, cmd.Hash) ? "cached" : "uncached", uri, cmd.Hash));

#if UNITY_2018_1_OR_NEWER
					req = UnityWebRequestAssetBundle.GetAssetBundle(uri, cmd.Hash, 0);
#else
					req = UnityWebRequest.GetAssetBundle(uri, cmd.Hash, 0);
#endif
				}

#if UNITY_2017_2_OR_NEWER
				req.SendWebRequest();
#else
				req.Send();
#endif

				while (!req.isDone)
				{
					yield return null;
				}

#if UNITY_2017_1_OR_NEWER
				var isNetworkError = req.isNetworkError;
				var isHttpError = req.isHttpError;
#else
				var isNetworkError = req.isError;
				var isHttpError = (req.responseCode < 200 || req.responseCode > 299) && req.responseCode != 0;  // 0 indicates the cached version may have been downloaded.  If there was an error then req.isError should have a non-0 code.
#endif

				if (isHttpError)
				{
					Debug.LogError(string.Format("Error downloading [{0}]: [{1}] [{2}]", uri, req.responseCode, req.error));

					if (retryCount < MAX_RETRY_COUNT && RETRY_ON_ERRORS.Contains(req.responseCode))
					{
						Debug.LogWarning(string.Format("Retrying [{0}] in [{1}] seconds...", uri, RETRY_WAIT_PERIOD));
						req.Dispose();
						activeDownloads--;
						yield return new WaitForSeconds(RETRY_WAIT_PERIOD);
						InternalHandle(Download(cmd, retryCount + 1));
						yield break;
					}
				}
				else if (isNetworkError)
				{
					Debug.LogError(string.Format("Error downloading [{0}]: [{1}]", uri, req.error));
				}
				else
				{
					try
					{
						bundle = DownloadHandlerAssetBundle.GetContent(req);
					}
					catch (Exception ex)
					{
						// Let the user know there was a problem and continue on with a null bundle.
						Debug.LogError("Error processing downloaded bundle, exception follows...");
						Debug.LogException(ex);
					}
				}

				if (!isNetworkError && !isHttpError && string.IsNullOrEmpty(req.error) && bundle == null)
				{
					if (cachingDisabled)
					{
						Debug.LogWarning(string.Format("There was no error downloading [{0}] but the bundle is null.  Caching has already been disabled, not sure there's anything else that can be done.  Returning...", uri));
					}
					else
					{
						Debug.LogWarning(string.Format("There was no error downloading [{0}] but the bundle is null.  Assuming there's something wrong with the cache folder, retrying with cache disabled now and for future requests...", uri));
						cachingDisabled = true;
						req.Dispose();
						activeDownloads--;
						yield return new WaitForSeconds(RETRY_WAIT_PERIOD);
						InternalHandle(Download(cmd, retryCount + 1));
						yield break;
					}
				}
			}

            try
			{
                cmd.OnComplete(bundle);
            }
			finally
			{
				if (req != null)
					req.Dispose();

                activeDownloads--;

                if (downloadQueue.Count > 0) {
                    InternalHandle(downloadQueue.Dequeue());
                }
            }
        }
    }
}
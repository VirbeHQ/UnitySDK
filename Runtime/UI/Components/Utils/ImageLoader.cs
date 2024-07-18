using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace UI.Scripts.Utils
{
    public class ImageLoader
    {
        public static IEnumerator GetRemoteTexture(string url, Action<Texture2D> textureLoaded)
        {
            using (UnityWebRequest www = UnityWebRequestTexture.GetTexture(url))
            {
                var asyncOp = www.SendWebRequest();

                while (asyncOp.isDone == false)
                {
                    yield return new WaitForEndOfFrame();
                }

                // read results:
                if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
                    // if( www.result!=UnityWebRequest.Result.Success )// for Unity >= 2020.1
                {
                    Debug.Log($"{www.error}, URL:{www.url}");
                    yield break;
                }
                else
                {
                    textureLoaded?.Invoke(DownloadHandlerTexture.GetContent(www));
                }
            }
        }
    }
}
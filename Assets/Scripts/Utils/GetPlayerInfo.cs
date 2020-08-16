using System;
using UnityEngine;
using UnityEngine.Networking;

namespace Utils
{

    public struct PlayerInfo
    {
        public int id;
        public string username;
        public int apples;

        public static void GetPlayerInfo(string hash, Action<PlayerInfo> callback)
        {
            WWWForm form = new WWWForm();
            form.AddField("hash", hash);
                        
            UnityWebRequest www = UnityWebRequest.Post(Constants.PHPServerHost + "/getPlayerInfo.php", form);
            www.SendWebRequest().completed += operation =>
            {
                PlayerInfo info = JsonUtility.FromJson<PlayerInfo>(www.downloadHandler.text);
                callback?.Invoke(info);
            };
        }
    }
}
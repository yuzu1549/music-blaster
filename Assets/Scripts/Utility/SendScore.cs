using UnityEngine;
using unityroom.Api;

namespace Ranking
{
    public class SendScore : MonoBehaviour
    {

        /// <summary>
        /// シャイニングスターのスコアをランキングに送る
        /// </summary>
        /// <param name="Score"></param>
        public static void SendShinigStarScore(int Score)
        {
            UnityroomApiClient.Instance.SendScore(1, Score, ScoreboardWriteMode.HighScoreDesc);
        }

        /// <summary>
        /// SnowDriveのスコアをランキングに送る
        /// </summary>
        /// <param name="Score"></param>
        public static void SendSnowDriveScore(int Score)
        {
            UnityroomApiClient.Instance.SendScore(2, Score, ScoreboardWriteMode.HighScoreDesc);
        }

    }
}

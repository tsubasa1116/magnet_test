using UnityEngine;

public class GameStart : MonoBehaviour
{
    private void Start()
    {
        GameManager.Instance.StartGame();
    }
}
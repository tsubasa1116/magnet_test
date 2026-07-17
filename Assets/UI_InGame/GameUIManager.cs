using UnityEngine;

public class GameUIManager : MonoBehaviour
{
    [SerializeField] private enemy_Boss bossScript;

    [SerializeField] private GameObject bossHPBar;
    [SerializeField] private GameObject bossName;

    void Start()
    {
        bossHPBar.SetActive(false);
        bossName.SetActive(false);
    }

    void Update()
    {
        if(bossScript.isStartAction)
        {
            bossHPBar.SetActive(true);
            bossName.SetActive(true);
        }
    }
}

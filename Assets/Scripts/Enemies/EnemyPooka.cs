using UnityEngine;

public class EnemyPooka : EnemyBase
{
    protected override void Start()
    {
        base.Start();
        normalSpeed = 2.5f;
    }
}
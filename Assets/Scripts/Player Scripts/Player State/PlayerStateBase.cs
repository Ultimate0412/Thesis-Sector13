using UnityEngine;

public abstract class PlayerStateBase
{
    protected PlayerController player;

    public PlayerStateBase(PlayerController player)
    {
        this.player = player;
    }

    public abstract void Enter();
    public abstract void UpdateState();
    public abstract void Exit();
}
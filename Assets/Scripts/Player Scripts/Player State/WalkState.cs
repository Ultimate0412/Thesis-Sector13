using UnityEngine;

public class WalkState : PlayerStateBase
{
    public WalkState(PlayerController player) : base(player) { }

    public override void Enter() { }

    public override void UpdateState()
    {
        player.ApplyMovement(player.walkSpeed);
        player.stats.ModifyStamina(player.stats.staminaRegenRate * Time.deltaTime);

        if (Input.GetAxis("Vertical") == 0 && Input.GetAxis("Horizontal") == 0)
        {
            player.SwitchState(player.idleState);
            return;
        }

        if (Input.GetKey(KeyCode.LeftShift) && !player.stats.IsExhausted)
        {
            player.SwitchState(player.runState);
            return;
        }
    }

    public override void Exit() { }
}
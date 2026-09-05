using UnityEngine;

public class RunState : PlayerStateBase
{
    public RunState(PlayerController player) : base(player) { }

    public override void Enter() { }

    public override void UpdateState()
    {
        player.ApplyMovement(player.runSpeed);

        // วิ่งลด Stamina
        player.stats.ModifyStamina(-player.stats.staminaDrainRate * Time.deltaTime);

        // ถ้าปล่อยปุ่ม Shift, น้ำหนักหมด, หรือหยุดเดิน ให้เปลี่ยน State
        if (!Input.GetKey(KeyCode.LeftShift) || player.stats.IsExhausted)
        {
            player.SwitchState(player.walkState);
            return;
        }

        if (Input.GetAxis("Vertical") == 0 && Input.GetAxis("Horizontal") == 0)
        {
            player.SwitchState(player.idleState);
            return;
        }
    }

    public override void Exit() { }
}
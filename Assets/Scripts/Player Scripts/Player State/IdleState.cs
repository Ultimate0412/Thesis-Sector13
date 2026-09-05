using UnityEngine;

public class IdleState : PlayerStateBase
{
    public IdleState(PlayerController player) : base(player) { }

    public override void Enter() { }

    public override void UpdateState()
    {
        // ฟื้นฟู Stamina ตอนยืนพัก
        player.stats.ModifyStamina(player.stats.staminaRegenRate * Time.deltaTime);

        // เช็คการเปลี่ยน State
        if (Input.GetKey(KeyCode.LeftShift) && Input.GetAxis("Vertical") > 0 && !player.stats.IsExhausted)
        {
            player.SwitchState(player.runState);
            return;
        }

        if (Input.GetAxis("Vertical") != 0 || Input.GetAxis("Horizontal") != 0)
        {
            player.SwitchState(player.walkState);
            return;
        }
    }

    public override void Exit() { }
}
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Drives one take of the Sector 13 loop for recording: walk, pull the lever,
/// pick up a parcel, scan it, shine the UV light, stamp it, ship it, end the day.
/// Movement is scripted (no input injection available) but the camera carries a
/// gentle handheld sway so it reads as a person rather than a dolly.
/// </summary>
public class DemoDirector : MonoBehaviour
{
    public float walkSpeed = 2.1f;
    public float handheldAmount = 1.1f;

    private Transform player;
    private Transform pivot;
    private PlayerInteractor interactor;
    private PlayerPickupSystem pickup;
    private PlayerController controller;

    private PackageBox held;
    private float handheldSeed;

    private IEnumerator Start()
    {
        handheldSeed = Random.value * 100f;
        player = GameObject.Find("[Player]/Player")?.transform;
        pivot = GameObject.Find("[Player]/Player/CameraPivot")?.transform;

        if (player == null || pivot == null) { Debug.LogError("[DemoDirector] player not found"); yield break; }

        interactor = FindAnyObjectByType<PlayerInteractor>();
        pickup = FindAnyObjectByType<PlayerPickupSystem>();
        controller = FindAnyObjectByType<PlayerController>();

        if (controller != null) controller.enabled = false;

        yield return new WaitForSeconds(1.0f);

        // ---- 1) look around at spawn
        Debug.Log("[DemoDirector] step1 spawn");
        yield return LookAround(1.6f);
        yield return new WaitForSeconds(0.4f);

        // ---- 2) walk to the delivery lever
        Debug.Log("[DemoDirector] step2 to lever");
        var lever = FindAnyObjectByType<ItemSpawner>();
        if (lever != null)
        {
            Debug.Log("[DemoDirector] walking to lever");
            yield return WalkTo(lever.transform.position + new Vector3(0f, 0f, 1.5f), 3.2f);
            Debug.Log("[DemoDirector] arrived at lever");
            yield return LookAtTarget(lever.transform.position + Vector3.up * 0.9f, 0.6f);
            yield return new WaitForSeconds(0.35f);
            if (interactor != null) ((IInteractable)lever).Interact(interactor);
            yield return new WaitForSeconds(1.4f);
        }

        // ---- 3) walk to a parcel and pick it up
        var boxes = FindObjectsByType<PackageBox>(FindObjectsSortMode.None);
        PackageBox target = null;
        foreach (var b in boxes) { target = b; break; }

        if (target != null)
        {
            yield return WalkTo(target.transform.position + new Vector3(0f, 0f, 1.05f), 2.6f);
            yield return LookAtTarget(target.transform.position + Vector3.up * 0.2f, 0.5f);
            yield return new WaitForSeconds(0.25f);
            yield return PickUp(target);
            yield return new WaitForSeconds(0.9f);
        }

        // ---- 4) tablet + scan
        var tablet = FindAnyObjectByType<InspectionTablet>();
        if (tablet != null && target != null)
        {
            tablet.DrawTablet();
            yield return new WaitForSeconds(0.8f);
            tablet.ScanPackage(target);
            yield return new WaitForSeconds(2.6f);
        }

        // ---- 5) UV reveal
        var uv = FindAnyObjectByType<UVFlashlightController>();
        if (uv != null)
        {
            if (held != null)
            {
                var applier = held.GetComponent<PackageTraceApplier>();
                if (applier != null) applier.isUVIlluminated = true;
            }
            uv.SetUVState(true);
            yield return new WaitForSeconds(2.4f);
            uv.SetUVState(false);
            if (held != null)
            {
                var applier = held.GetComponent<PackageTraceApplier>();
                if (applier != null) applier.isUVIlluminated = false;
            }
            yield return new WaitForSeconds(0.6f);
        }

        // ---- 6) stamp it
        var stamp = StampUIController.Instance;
        if (stamp != null && target != null)
        {
            stamp.OpenStampMenu(target);
            yield return new WaitForSeconds(0.9f);
            stamp.OnClickApprove();
            yield return new WaitForSeconds(0.8f);
        }

        // ---- 7) carry it to the shipping bay and send it
        var ship = FindAnyObjectByType<ShippingDropPoint>();
        if (ship != null && held != null)
        {
            yield return WalkTo(ship.transform.position + new Vector3(0f, 0f, 1.6f), 3.4f);
            yield return LookAtTarget(ship.transform.position + Vector3.up * 0.4f, 0.5f);
            yield return new WaitForSeconds(0.3f);

            ship.PlaceItem(held.gameObject, held.Weight);
            yield return new WaitForSeconds(0.7f);

            var mi = ship.GetType().GetMethod("ProcessShipping", BindingFlags.NonPublic | BindingFlags.Instance);
            if (mi != null) mi.Invoke(ship, null);
            yield return new WaitForSeconds(1.6f);
        }

        // ---- 8) end of day + summary
        var gd = GameDayManager.Instance;
        if (gd != null)
        {
            gd.RequestEndDay(true);
            yield return new WaitForSeconds(3.2f);
        }

        yield return new WaitForSeconds(2.0f);

        Debug.Log("[DemoDirector] sequence done");
        var cap = FindAnyObjectByType<DemoCapture>();
        if (cap != null) cap.Close();
    }

    private void Update()
    {
        if (pivot == null) return;
        // subtle handheld sway on top of whatever rotation the director set
        float t = Time.time + handheldSeed;
        float yaw = (Mathf.PerlinNoise(t * 0.55f, 0.13f) - 0.5f) * handheldAmount;
        float pitch = (Mathf.PerlinNoise(0.31f, t * 0.48f) - 0.5f) * handheldAmount * 0.7f;
        pivot.Rotate(Vector3.up, yaw * Time.deltaTime * 12f, Space.World);
        pivot.Rotate(Vector3.right, pitch * Time.deltaTime * 10f, Space.World);
    }

    // ------------------------------------------------------------------ helpers

    private IEnumerator LookAround(float duration)
    {
        if (pivot == null) yield break;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            pivot.Rotate(Vector3.up, 14f * Time.deltaTime, Space.World);
            yield return null;
        }
    }

    private IEnumerator LookAtTarget(Vector3 worldPos, float duration)
    {
        if (pivot == null) yield break;
        var startRot = pivot.rotation;
        var targetRot = Quaternion.LookRotation(worldPos - pivot.position, Vector3.up);
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            pivot.rotation = Quaternion.Slerp(startRot, targetRot, Mathf.Clamp01(t / duration));
            yield return null;
        }
        pivot.rotation = targetRot;
    }

    private IEnumerator WalkTo(Vector3 destination, float duration)
    {
        if (player == null) yield break;
        var start = player.position;
        yield return LookAtTarget(new Vector3(destination.x, player.position.y + 1.4f, destination.z), 0.5f);

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            // ease in / ease out so it reads as a person starting and stopping
            float e = k < 0.5f ? 2f * k * k : 1f - Mathf.Pow(-2f * k + 2f, 2f) * 0.5f;
            player.position = Vector3.Lerp(start, destination, e);
            // small head bob while moving
            if (pivot != null)
                pivot.position = pivot.parent.position + Vector3.up * (0.02f * Mathf.Sin(t * 11f));
            yield return null;
        }
        player.position = destination;
        if (pivot != null) pivot.localPosition = new Vector3(0f, 0.02f, 0f);
    }

    private IEnumerator PickUp(PackageBox box)
    {
        if (box == null || pickup == null) yield break;
        var cam = Camera.main;
        if (cam == null) yield break;

        Vector3 aim = box.transform.position + Vector3.up * 0.2f;
        var ray = new Ray(cam.transform.position, (aim - cam.transform.position).normalized);
        var mi = pickup.GetType().GetMethod("TryPickUp", BindingFlags.NonPublic | BindingFlags.Instance);
        if (mi != null)
        {
            mi.Invoke(pickup, new object[] { ray });
            yield return new WaitForSeconds(0.4f);
            held = pickup.heldObject != null ? pickup.heldObject.GetComponent<PackageBox>() : null;
        }
    }
}

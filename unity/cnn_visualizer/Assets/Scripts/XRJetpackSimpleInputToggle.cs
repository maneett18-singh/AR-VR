using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// Toggle jetpack active and unequip using XR InputDevices (B/Y for unequip).
/// Attach to the XR Origin root.
/// </summary>
[RequireComponent(typeof(XRJetpackSimpleController))]
public class XRJetpackSimpleInputToggle : MonoBehaviour
{
    [Header("References")]
    public XRJetpackSimpleController jetpack;

    private bool _lastTogglePressed;
    private bool _lastUnequipPressed;

    private void Awake()
    {
        if (jetpack == null)
            jetpack = GetComponent<XRJetpackSimpleController>();

        if (jetpack == null)
            jetpack = GetComponentInParent<XRJetpackSimpleController>();
    }

    private void Update()
    {
        if (jetpack == null || !jetpack.hasJetpack)
            return;

    bool togglePressed = ReadPrimary2DAxisClick(XRNode.LeftHand) || ReadPrimary2DAxisClick(XRNode.RightHand);
    bool unequipPressed = ReadSecondaryButton(XRNode.RightHand);

        if (togglePressed && !_lastTogglePressed)
            jetpack.SetActive(!jetpack.jetpackActive);

        if (unequipPressed && !_lastUnequipPressed)
            jetpack.Unequip();

        _lastTogglePressed = togglePressed;
        _lastUnequipPressed = unequipPressed;
    }

    private bool ReadPrimary2DAxisClick(XRNode node)
    {
        var device = InputDevices.GetDeviceAtXRNode(node);
        if (!device.isValid)
            return false;

        if (device.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out bool pressed))
            return pressed;

        return false;
    }

    private bool ReadSecondaryButton(XRNode node)
    {
        var device = InputDevices.GetDeviceAtXRNode(node);
        if (!device.isValid)
            return false;

        if (device.TryGetFeatureValue(CommonUsages.secondaryButton, out bool pressed))
            return pressed;

        return false;
    }
}

/// <summary>
///     Shared resolution for which hold-aim weapon the aim presentation (<see cref="BowAimCamera" />,
///     <see cref="BowCrosshairUI" />, <see cref="BowReloadUI" />) should poll: the serialized bow
///     while it's the active class's weapon (still enabled), else whatever
///     <see cref="IChargedAimWeapon" /> the class controller activated (the Berserker's thrown axe),
///     else the bow found on the player — legacy wiring without a class controller, where even a
///     disabled bow is a valid poll target (its IsAiming just stays false).
/// </summary>
public static class AimWeaponResolver
{
    public static IChargedAimWeapon Resolve(PlayerBow serializedBow)
    {
        if (serializedBow != null && serializedBow.enabled)
        {
            return serializedBow;
        }

        if (Player.Instance != null)
        {
            PlayerClassController classController = UnityEngine.Object.FindAnyObjectByType<PlayerClassController>();
            if (classController != null && classController.ActiveAimWeapon != null)
            {
                return classController.ActiveAimWeapon;
            }
            if (serializedBow == null)
            {
                serializedBow = Player.Instance.GetComponentInChildren<PlayerBow>();
            }
        }

        return serializedBow;
    }
}

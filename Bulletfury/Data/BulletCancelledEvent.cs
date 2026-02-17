using System;
using UnityEngine.Events;

namespace BulletFury.Data
{
    /// <summary>
    /// Event fired when a module cancels bullet death.
    /// Bool parameter indicates whether the death was caused by collision.
    /// </summary>
    [Serializable]
    public class BulletCancelledEvent : UnityEvent<int, BulletContainer, bool>
    { }
}

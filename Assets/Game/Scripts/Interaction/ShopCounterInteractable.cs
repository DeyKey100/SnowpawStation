using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Assets.Game.Scripts.Interaction
{
    /// <summary>Стойка с рычагом кассы: E — подтвердить товар (товар → клиенту).</summary>
    [DisallowMultipleComponent]
    public sealed class ShopCounterInteractable : MonoBehaviour, IInteractable
    {
        public string GetInteractPrompt()
        {
            ServiceDirector service = ServiceDirector.Instance;
            return service != null && !string.IsNullOrEmpty(service.CarriedGood)
                ? $"[E] Подтвердить: {Goods.DisplayName(service.CarriedGood)} (касса)"
                : "[E] Стойка с кассой";
        }

        public void Interact()
        {
            ServiceDirector.Instance?.ConfirmOnCounter();
        }
    }

}

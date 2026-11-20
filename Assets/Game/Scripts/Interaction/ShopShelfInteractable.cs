using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Assets.Game.Scripts.Interaction
{
    /// <summary>Полка магазина: E — взять нужный товар (кофе, печенье, сигареты, грелка...).</summary>
    [DisallowMultipleComponent]
    public sealed class ShopShelfInteractable : MonoBehaviour, IInteractable
    {
        public string GetInteractPrompt()
        {
            ServiceDirector service = ServiceDirector.Instance;
            if (service == null || service.CurrentOrder == null || string.IsNullOrEmpty(service.CurrentOrder.item))
            {
                return "[E] Полка с товаром";
            }

            return !string.IsNullOrEmpty(service.CarriedGood)
                ? $"В лапах: {Goods.DisplayName(service.CarriedGood)} — несите на стойку"
                : $"[E] Взять {Goods.DisplayName(service.CurrentOrder.item)} с полки";
        }

        public void Interact()
        {
            ServiceDirector service = ServiceDirector.Instance;
            if (service == null || service.CurrentOrder == null)
            {
                return;
            }

            string need = service.CurrentOrder.item;
            if (string.IsNullOrEmpty(need) || !string.IsNullOrEmpty(service.CarriedGood))
            {
                return;
            }

            service.TakeGood(need);
        }
    }
}

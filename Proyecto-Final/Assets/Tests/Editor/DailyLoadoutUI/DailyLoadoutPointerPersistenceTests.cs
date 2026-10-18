using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using F = DailyLoadoutUIFixture;

public sealed class DailyLoadoutPointerPersistenceTests
{
    [TestCase(true)] [TestCase(false)]
    public void UnchangedAvailabilityPreservesHoverAndPressAcrossPresenterRefresh(bool equipmentSlot)
    {
        using (var f = new F())
        {
            Assert.That(f.Open(), Is.True); f.Select(true, 0);
            var target = equipmentSlot ? f.Slot(true, 0) : f.Option(0);
            var pointer = new PointerEventData(null) { button = PointerEventData.InputButton.Left };
            F.Call(target, "OnPointerEnter", pointer);
            if (equipmentSlot) Assert.That(F.State(target), Does.Contain("Highlighted"));
            else Assert.That(F.State(target), Is.EqualTo("Available"));
            f.Refresh();
            if (equipmentSlot) Assert.That(F.State(target), Does.Contain("Highlighted"));
            else Assert.That(F.State(target), Is.EqualTo("Available"));
            Assert.That(((Image)F.Field(target, "background")).sprite, Is.SameAs(F.Field(target, "hoverSprite")));
            F.Call(target, "OnPointerDown", pointer); f.Refresh();
            Assert.That(((Image)F.Field(target, "background")).sprite, Is.SameAs(F.Field(target, "pressedSprite")));
            F.Call(target, "SetReadOnly", true);
            Assert.That(F.SelectButton(target).interactable, Is.False);
            Assert.That(((Image)F.Field(target, "background")).sprite, Is.SameAs(F.Field(target, "normalSprite")));
        }
    }
}

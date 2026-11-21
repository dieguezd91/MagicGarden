using System;
using System.IO;
using System.Reflection;
using MagicGarden;
using NUnit.Framework;
using UnityEngine;

public sealed class UpgradeBalanceEditorTests
{
    [Test]
    public void SaveWritesRuntimeCompatibleBalanceAndRejectsStaleDraft()
    {
        var window = Type.GetType("UpgradeBalanceWindow, Assembly-CSharp-Editor", throwOnError: true);
        var write = window.GetMethod("WriteBalance", BindingFlags.Static | BindingFlags.NonPublic);
        string path = Path.GetTempFileName();
        try
        {
            string original = Resources.Load<TextAsset>("MagicGardenBalance").text;
            File.WriteAllText(path, original);
            var draft = JsonUtility.FromJson<Balance>(original);
            draft.rarities[0].bonus = 0.125f;
            draft.profiles[0].ratios[0] = 0.75f;
            draft.specials[0].costs[0].amount = 220;
            string edited = JsonUtility.ToJson(draft, true);

            write.Invoke(null, new object[] { path, original, edited });
            var saved = JsonUtility.FromJson<Balance>(File.ReadAllText(path));
            Assert.That(saved.rarities[0].bonus, Is.EqualTo(0.125f));
            Assert.That(saved.profiles[0].Ratio(Stat.MaxHealth), Is.EqualTo(0.75f));
            Assert.That(saved.specials[0].costs[0].amount, Is.EqualTo(220));

            var failure = Assert.Throws<TargetInvocationException>(() =>
                write.Invoke(null, new object[] { path, original, original }));
            Assert.That(failure.InnerException, Is.TypeOf<IOException>());
            Assert.That(File.ReadAllText(path), Is.EqualTo(edited));
        }
        finally
        {
            File.Delete(path);
        }
    }
}

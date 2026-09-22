using grapher.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace grapher_tests;

[TestClass]
public class ProfileDialogViewModelTests
{
    [TestMethod]
    public void NameIsValidatedAsItChanges()
    {
        var dialog = ProfileDialogViewModel.ForName("New profile", "Name it.", "Create", "fast", n => n == "taken" ? "Another profile already has this name." : null);

        Assert.IsTrue(dialog.AsksForName);
        Assert.IsTrue(dialog.CanConfirm);

        dialog.Name = "taken";

        Assert.IsFalse(dialog.CanConfirm);
        Assert.IsTrue(dialog.HasNameError);
        Assert.AreEqual("Another profile already has this name.", dialog.NameError);

        dialog.Name = "free";

        Assert.IsTrue(dialog.CanConfirm);
        Assert.IsNull(dialog.NameError);
    }

    [TestMethod]
    public void InvalidInitialNameCannotBeConfirmed()
    {
        var dialog = ProfileDialogViewModel.ForName("New profile", "Name it.", "Create", string.Empty, n => n.Length == 0 ? "Enter a name." : null);

        Assert.IsFalse(dialog.CanConfirm);
    }

    [TestMethod]
    public void ConfirmationDoesNotAskForName()
    {
        var dialog = ProfileDialogViewModel.ForConfirmation("Delete profile", "Delete it?", "Delete");

        Assert.IsFalse(dialog.AsksForName);
        Assert.IsTrue(dialog.CanConfirm);
        Assert.AreEqual("Delete", dialog.ConfirmText);
    }
}

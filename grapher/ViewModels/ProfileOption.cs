using System;
using System.Threading.Tasks;

namespace grapher.ViewModels;

public sealed record ProfileOption(string Name, string Label);

public sealed record ProfileDialogRequest(ProfileDialogViewModel Dialog, Func<string, Task> Confirm);

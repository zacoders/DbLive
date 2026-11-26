---
created: 2026-11-03
---
# The `dotnet new dblive` template in the Visual Studio "New Project" dialog

## Idea
The template of 0096-30 is made for the CLI: `dotnet new install DbLive.Templates`, then `dotnet new dblive -n MyDb --provider mssql|postgresql`. Visual Studio may list an installed `dotnet new` template in its "New Project" dialog. Without `.template.config/ide.host.json` it does not show the `--provider` choice, so a project created there is always SQL Server.

Came from the open question of 0096-30. Decided 2026-11-03 by the maintainer: not in 0096-30, because CI cannot check the dialog.

## Open questions
- [ ] Does Visual Studio (2022, 18) show a template with `"type": "solution"`, and what does it do with the template's `.slnx` next to the solution it creates? `primaryOutputs` in `template.json` names the projects to add.
- [ ] `ide.host.json`: the `provider` symbol as a dropdown, an icon. Who checks it by hand in Visual Studio before a release?
- [ ] Rider: does it need a host file of its own?

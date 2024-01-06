
# How this nuget was build

There is static content in SQL folder. It should be stored in the NuGet and copied to the output folder on build. It is not possible to configure only in `.csproj` file. Actually it is possible, you can copy content to the `contentFiles` and `content` folders in the NuGet, but once you install such NuGet this content will be displayed in solution explored under the project, and this behavior cannot be changed.

What we actually need is just to copy static content to the output folder during the build process. This can be solved by `project.targets` file added to the NuGet. It will be executed on the build of the project where NuGet installed. There is action will copy static stuff to output folder.

## Notes
* `%(RecursiveDir)%(Filename)%(Extension)` — used to preserve folders structure.
* `.csproj` there is a step to copy SQL content to output build folder, this needed for development only, and will not affect NuGet contents.

## Links

* [Copy NuGet Content Files to Output Directory on Build](https://blog.tonysneed.com/2021/12/04/copy-nuget-content-files-to-output-directory-on-build/)
* [Prevent duplicating files in NuGet content and contentFiles folders](https://stackoverflow.com/a/47469709/1151431)


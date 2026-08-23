# Third-party software notices

XTimelineViewer Kotsume Edition includes redistributable components from Microsoft, the .NET Foundation,
CommunityToolkit contributors, and other open-source projects.

The release package includes a `licenses` directory generated from the exact packages restored for that build.
It contains the license and notice files supplied by NuGet packages, together with the .NET runtime license and
third-party notices. Do not remove that directory when redistributing the portable archive or installer payload.

Main component families include:

- .NET Runtime — MIT and third-party licenses
- Microsoft Windows App SDK — Microsoft Software License Terms and third-party notices
- Microsoft Edge WebView2 SDK — BSD 3-Clause and third-party notices
- .NET Community Toolkit / CommunityToolkit.WinUI — MIT and third-party notices
- System.* libraries redistributed with the self-contained build — MIT and third-party notices

The project itself is distributed under the MIT License in `LICENSE`.
The Windows installer presents the Microsoft Windows App SDK redistribution terms before installation. The same
terms are available after installation as `licenses/Microsoft-WindowsAppSDK-LICENSE.txt`.

The generated `licenses/PACKAGES.txt` records the exact NuGet package IDs and versions used by the build. A package
with no standalone license file in its NuGet directory remains subject to the license expression or license URL in
its NuGet metadata and to the notices supplied by its parent runtime or SDK distribution.

Microsoft product names and trademarks belong to Microsoft. X and related names and marks belong to X Corp.
Their inclusion does not imply sponsorship or endorsement.

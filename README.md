# Pail

<p align="center">
  <img src="src/Pail.App/Assets/Square150x150Logo.scale-200.png" width="250"/>
</p>

**Pail is a Windows WinUI 3 app for browsing Amazon S3 buckets, and downloading objects and folders.**

## Main features

- Sign in with access key and secret key, an optional session token, pasted AWS Console credentials, or the AWS default credential chain
- Load local AWS profiles, choose a specific profile, or stay on the automatic (default) profile
- Browse S3 buckets, search bucket names, and copy selected bucket names to the clipboard
- Navigate bucket folders, inspect object names, sizes, and last-modified timestamps
- Control the number of items loaded, and load more results for large listings
- Copy object names or full S3 keys from the object browser
- Download one or more files or folders, using a configurable default download folder or a prompt for each download
- Save app preferences for theme, default region, preferred profile, object browser load sizes, and status message timing

## How to use it

1. Start the app.
2. Choose a region and your sign-in method.
3. Enter credentials, paste AWS Console credentials, or use the default credential chain with an AWS profile.
4. Sign in to list your buckets.
5. Search or open a bucket, browse folders, then copy or download the items you need.
6. Open Settings to adjust theme, login defaults, object loading behaviour, and download preferences.

## Download

The app is published as a self-contained single-file executable for `win-x64`, `win-x86`, and `win-arm64`.

Browse the [releases](https://github.com/TheBlueSky/Pail/releases) to download the latest version.

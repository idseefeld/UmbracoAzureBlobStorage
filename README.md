Azure Blob Storage for Umbraco
==============================
This code was inspired by Johannes Müller.

This is intended to replaces the Umbraco.Core.IO.PhysicalFileSystem for media files. 
All files will be stored in the configured Azure storage as blobs.

Some Hints for Testing
-----------------------
To run the tests in Visual Studio 2012 you need and extension like NuGet package "NUnit Test Adapter" in your Visual Studio toolbox.
And of course you have to make sure the Microsoft Azure SDK is installed and the MS Azure Storage Emulator is running. 
Sometimes I found it useful to delete the local database "WAStorageEmulatorDb34" for the emulator. 
After deleting the database via Sql Server Manager you can create a new one with three command lines. 
First move to the emulator install folder (on my machine "C:\Program Files (x86)\Microsoft SDKs\Azure\Storage Emulator").
My local sql server instance is called "SQLEXPRESS2008".
In your command line tool enter:

cd C:\Program Files (x86)\Microsoft SDKs\Azure\Storage Emulator
WAStorageEmulator stop
WAStorageEmulator init -sqlinstance SQLEXPRESS2008
WAStorageEmulator start

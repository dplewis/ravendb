using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Raven.Abstractions.Data;
using Raven.Abstractions.Database.Smuggler.FileSystem;
using Raven.Abstractions.Exceptions;
using Raven.Abstractions.FileSystem;
using Raven.Client.FileSystem;
using Raven.Client.FileSystem.Extensions;
using Raven.Database.Extensions;
using Raven.Json.Linq;
using Raven.Smuggler.FileSystem;
using Raven.Smuggler.FileSystem.Files;
using Raven.Smuggler.FileSystem.Remote;
using Raven.Tests.Common;
using Raven.Tests.Common.Util;
using Xunit;
using Xunit.Extensions;

namespace Raven.Tests.FileSystem.Smuggler
{
    public partial class SmugglerExecutionTests : RavenFilesTestWithLogs
    {
        [Fact, Trait("Category", "Smuggler")]
        public async Task ShouldThrowIfFileSystemDoesNotExist()
        {
            using (var store = NewStore())
            {
                var server = GetServer();
                var outputDirectory = Path.Combine(server.Configuration.Core.DataDirectory, "Export");

                Directory.CreateDirectory(outputDirectory);

                try
                {
                    var smuggler = new FileSystemSmuggler(new FileSystemSmugglerOptions());

                    var remoteConnectionOptions = new FilesConnectionStringOptions
                    {
                        Url = store.Url,
                        DefaultFileSystem = "DoesNotExist"
                    };

                    var message = string.Format("Smuggler does not support file system creation (file system 'DoesNotExist' on server '{0}' must exist before running Smuggler).", store.Url);

                    var e = await AssertAsync.Throws<SmugglerException>(() => smuggler.ExecuteAsync(new RemoteSmugglingSource(remoteConnectionOptions), new FileSmugglingDestination(outputDirectory.ToFullPath(), false)));
                    Assert.Equal(message, e.Message);

                    e = await AssertAsync.Throws<SmugglerException>(() => smuggler.ExecuteAsync(new FileSmugglingSource(outputDirectory.ToFullPath()), new RemoteSmugglingDestination(remoteConnectionOptions)));
                    Assert.Equal(message, e.Message);
                }
                finally
                {
                    IOExtensions.DeleteDirectory(outputDirectory);
                }
            }
        }

        [Trait("Category", "Smuggler")]
        [Theory]
        [PropertyData("Storages")]
        public async Task ShouldNotThrowIfFileSystemExists(string storage)
        {
            using (var store = NewStore(fileSystemName: "DoesExists", requestedStorage: storage))
            {
                var server = GetServer();
                var exportFile = Path.Combine(server.Configuration.Core.DataDirectory, "Export");

                await store.AsyncFilesCommands.Admin.EnsureFileSystemExistsAsync(store.DefaultFileSystem);
                await InitializeWithRandomFiles(store, 2, 4);

                var smuggler = new FileSystemSmuggler(new FileSystemSmugglerOptions());

                var remoteConnectionOptions = new FilesConnectionStringOptions
                {
                    Url = store.Url,
                    DefaultFileSystem = store.DefaultFileSystem
                };

                var result = await smuggler.ExecuteAsync(new RemoteSmugglingSource(remoteConnectionOptions), new FileSmugglingDestination(exportFile.ToFullPath(), false));
                await smuggler.ExecuteAsync(new FileSmugglingSource(result.OutputPath), new RemoteSmugglingDestination(remoteConnectionOptions));
            }
        }

        [Fact, Trait("Category", "Smuggler")]
        public async Task ShouldNotThrowIfFileSystemExistsUsingDefaultConfiguration()
        {
            using (var store = NewStore())
            {
                var server = GetServer();
                var exportFile = Path.Combine(server.Configuration.Core.DataDirectory, "Export");

                await store.AsyncFilesCommands.Admin.EnsureFileSystemExistsAsync(store.DefaultFileSystem);
                await InitializeWithRandomFiles(store, 1, 100);

                var smuggler = new FileSystemSmuggler(new FileSystemSmugglerOptions());

                var export = await smuggler.ExecuteAsync(new RemoteSmugglingSource(new FilesConnectionStringOptions
                                                                                    {
                                                                                        Url = store.Url,
                                                                                        DefaultFileSystem = store.DefaultFileSystem
                                                                                    }), 
                                                                                    new FileSmugglingDestination(exportFile.ToFullPath(), false));
                    
                await smuggler.ExecuteAsync(new FileSmugglingSource(exportFile.ToFullPath()), new RemoteSmugglingDestination(new FilesConnectionStringOptions()
                                                                                                                            {
                                                                                                                                Url = store.Url,
                                                                                                                                DefaultFileSystem = store.DefaultFileSystem
                                                                                                                            }));
                        
                await VerifyDump(store, export.OutputPath, s =>
                {
                    using (var session = s.OpenAsyncSession())
                    {
                        var files = s.AsyncFilesCommands.BrowseAsync().Result;
                        Assert.Equal(1, files.Count());
                    }
                });
            }
        }

        [Fact, Trait("Category", "Smuggler")]
        public async Task BehaviorWhenServerIsDown()
        {
            using (var store = NewStore())
            {
                var server = GetServer();
                var exportFile = Path.Combine(server.Configuration.Core.DataDirectory, "Export");

                // create empty zip file to imitate empty export file 
                using (var file = File.Create(exportFile))
                using (new ZipArchive(file, ZipArchiveMode.Create))
                {
                }

                var connectionOptions = new FilesConnectionStringOptions { Url = "http://localhost:8078/", DefaultFileSystem = store.DefaultFileSystem };

                var smuggler = new FileSystemSmuggler(new FileSystemSmugglerOptions());

                var e = await AssertAsync.Throws<SmugglerException>(() => smuggler.ExecuteAsync(new FileSmugglingSource(exportFile), new RemoteSmugglingDestination(connectionOptions)));

                Assert.Contains("Smuggler encountered a connection problem:", e.Message);

                e = await AssertAsync.Throws<SmugglerException>(() => smuggler.ExecuteAsync(new RemoteSmugglingSource(connectionOptions), new FileSmugglingDestination(exportFile, false)));
                Assert.Contains("Smuggler encountered a connection problem:", e.Message);
            }
        }

        [Fact, Trait("Category", "Smuggler")]
        public async Task CanDumpEmptyFileSystem()
        {
            using (var store = NewStore())
            {
                var server = GetServer();
                var outputDirectory = Path.Combine(server.Configuration.Core.DataDirectory, "Export");
                try
                {

                    // now perform full backup
                    var smuggler = new FileSystemSmuggler(new FileSystemSmugglerOptions());
                    
                    var export = await smuggler.ExecuteAsync(
                        new RemoteSmugglingSource(new FilesConnectionStringOptions
                        {
                            Url = server.Url,
                            DefaultFileSystem = store.DefaultFileSystem,
                        }),
                        new FileSmugglingDestination(outputDirectory, true));

                    await VerifyDump(store, export.OutputPath, s =>
                    {
                        using (var session = s.OpenAsyncSession())
                        {
                            var files = s.AsyncFilesCommands.BrowseAsync().Result;
                            Assert.Equal(0, files.Count());
                        }
                    });
                }
                finally
                {
                    IOExtensions.DeleteDirectory(outputDirectory);
                }
            }
        }

        [Fact, Trait("Category", "Smuggler")]
        public async Task CanHandleFilesExceptionsGracefully()
        {
            using (var store = NewStore())
            {
                await store.AsyncFilesCommands.GetStatisticsAsync();

                var server = GetServer();
                var outputDirectory = Path.Combine(server.Configuration.Core.DataDirectory, "Export");

                var alreadyReset = false;
                var proxyPort = 8070;
                var forwarder = new ProxyServer(ref proxyPort, server.Configuration.Core.Port)
                {
                    VetoTransfer = (totalRead, buffer) =>
                    {
                        if (alreadyReset == false && totalRead > 28000)
                        {
                            alreadyReset = true;
                            return true;
                        }
                        return false;
                    }
                };

                try
                {
                    ReseedRandom(100); // Force a random distribution.

                    await InitializeWithRandomFiles(store, 50, 30);

                    // now perform full backup
                    var smuggler = new FileSystemSmuggler(new FileSystemSmugglerOptions());

                    FileSystemSmugglerOperationState exportResult = null;
                    try
                    {
                        // We will ensure this one will fail somewhere along the line.
                        exportResult = await smuggler.ExecuteAsync(
                            new RemoteSmugglingSource(new FilesConnectionStringOptions
                            {
                                Url = "http://localhost:" + proxyPort,
                                DefaultFileSystem = store.DefaultFileSystem,
                            }),
                            new FileSmugglingDestination(outputDirectory, true));
                    }
                    catch (SmugglerException inner)
                    {
                        exportResult = new FileSystemSmugglerOperationState
                        {
                            OutputPath = inner.File
                        };
                    }

                    Assert.NotNull(exportResult);
                    Assert.True(!string.IsNullOrWhiteSpace(exportResult.OutputPath));

                    // Continue with the incremental dump.
                    exportResult = await smuggler.ExecuteAsync(
                        new RemoteSmugglingSource(new FilesConnectionStringOptions
                        {
                            Url = server.Url,
                            DefaultFileSystem = store.DefaultFileSystem,
                        }),
                        new FileSmugglingDestination(outputDirectory, true));

                    // Import everything and verify all files are there. 
                    await VerifyDump(store, outputDirectory, s =>
                    {
                        using (var session = s.OpenAsyncSession())
                        {
                            var files = s.AsyncFilesCommands.BrowseAsync().Result;
                            Assert.Equal(50, files.Count());
                        }

                    });
                }
                finally
                {
                    forwarder.Dispose();
                    IOExtensions.DeleteDirectory(outputDirectory);
                }
            }
        }

        [Fact, Trait("Category", "Smuggler")]
        public async Task OnlyActiveContentIsPreserved_MultipleDirectories()
        {
            using (var store = NewStore())
            {
                var server = GetServer();
                var outputDirectory = Path.Combine(server.Configuration.Core.DataDirectory, "Export");

                await store.AsyncFilesCommands.Admin.EnsureFileSystemExistsAsync(SourceFilesystem);
                await store.AsyncFilesCommands.Admin.EnsureFileSystemExistsAsync(DestinationFilesystem);

                int total = 10;
                var files = new string[total];
                using (var session = store.OpenAsyncSession(SourceFilesystem))
                {
                    for (int i = 0; i < total; i++)
                    {
                        files[i] = i + "/test.file";
                        session.RegisterUpload(files[i], CreateRandomFileStream(100 * i + 12));
                    }

                    await session.SaveChangesAsync();
                }

                int deletedFiles = 0;
                var rnd = new Random();
                using (var session = store.OpenAsyncSession(SourceFilesystem))
                {
                    for (int i = 0; i < total; i++)
                    {
                        if (rnd.Next(2) == 0)
                        {
                            session.RegisterFileDeletion(files[i]);
                            deletedFiles++;
                        }
                    }

                    await session.SaveChangesAsync();
                }

                // now perform full backup
                var smuggler = new FileSystemSmuggler(new FileSystemSmugglerOptions());

                var export = await smuggler.ExecuteAsync(
                    new RemoteSmugglingSource(new FilesConnectionStringOptions
                    {
                        Url = server.Url,
                        DefaultFileSystem = SourceFilesystem
                    }), 
                    new FileSmugglingDestination(outputDirectory, true));

                await VerifyDump(store, export.OutputPath, s =>
                {
                    using (var session = s.OpenAsyncSession())
                    {
                        int activeFiles = 0;
                        for (int i = 0; i < total; i++)
                        {
                            var file = session.LoadFileAsync(files[i]).Result;
                            if (file != null)
                                activeFiles++;
                        }

                        Assert.Equal(total - deletedFiles, activeFiles);
                    }
                });
            }
        }

        [Fact]
        [Trait("Category", "Smuggler")]
        public async Task ContentIsPreserved_SingleFile()
        {
            using (var store = NewStore())
            {
                ReseedRandom(100); // Force a random distribution.

                int fileSize = 10000;

                var server = GetServer();
                var exportFile = Path.Combine(server.Configuration.Core.DataDirectory, "Export");

                var smuggler = new FileSystemSmuggler(new FileSystemSmugglerOptions());

                var fileContent = CreateRandomFileStream(fileSize);

                using (var session = store.OpenAsyncSession())
                {
                    session.RegisterUpload("test1.file", fileContent);
                    await session.SaveChangesAsync();
                }

                FileSystemSmugglerOperationState result;
                using (new FilesStore { Url = server.Url }.Initialize())
                {
                    // now perform full backup                    
                    result = await smuggler.ExecuteAsync(
                        new RemoteSmugglingSource(new FilesConnectionStringOptions { Url = server.Url, DefaultFileSystem = store.DefaultFileSystem, }), 
                        new FileSmugglingDestination(exportFile, false));
                }

                await VerifyDump(store, result.OutputPath, s =>
                {
                    fileContent.Position = 0;
                    using (var session = s.OpenAsyncSession())
                    {
                        var file = session.LoadFileAsync("test1.file").Result;

                        Assert.Equal(fileSize, file.TotalSize);

                        var stream = session.DownloadAsync(file).Result;

                        Assert.Equal(fileContent.GetHashAsHex(), stream.GetHashAsHex());
                    }
                });
            }
        }

        [Fact, Trait("Category", "Smuggler")]
        public async Task ContentIsPreserved_MultipleFiles()
        {
            using (var store = NewStore())
            {
                ReseedRandom(100); // Force a random distribution.

                var server = GetServer();
                var exportFile = Path.Combine(server.Configuration.Core.DataDirectory, "Export");

                var smuggler = new FileSystemSmuggler(new FileSystemSmugglerOptions());

                var files = new Stream[10];
                using (var session = store.OpenAsyncSession())
                {
                    for (int i = 0; i < files.Length; i++)
                    {
                        files[i] = CreateRandomFileStream(100 * i + 12);
                        session.RegisterUpload("test" + i + ".file", files[i]);
                    }

                    await session.SaveChangesAsync();
                }

                FileSystemSmugglerOperationState result;
                using (new FilesStore { Url = server.Url }.Initialize())
                {
                    // now perform full backup                    
                    result = await smuggler.ExecuteAsync(
                        new RemoteSmugglingSource(new FilesConnectionStringOptions
                        {
                            Url = server.Url,
                            DefaultFileSystem = store.DefaultFileSystem,
                        }),
                        new FileSmugglingDestination(exportFile, false));
                }

                await VerifyDump(store, result.OutputPath, s =>
                {
                    for (int i = 0; i < files.Length; i++)
                    {
                        using (var session = s.OpenAsyncSession())
                        {
                            var file = session.LoadFileAsync("test" + i + ".file").Result;
                            var stream = session.DownloadAsync(file).Result;

                            files[i].Position = 0;
                            Assert.Equal(files[i].GetHashAsHex(), stream.GetHashAsHex());
                        }
                    }
                });
            }
        }

        [Fact, Trait("Category", "Smuggler")]
        public async Task ContentIsPreserved_MultipleDirectories()
        {
            using (var store = NewStore())
            {
                ReseedRandom(100); // Force a random distribution.

                var server = GetServer();
                var exportFile = Path.Combine(server.Configuration.Core.DataDirectory, "Export");

                var smuggler = new FileSystemSmuggler(new FileSystemSmugglerOptions());

                var files = new Stream[10];
                using (var session = store.OpenAsyncSession())
                {
                    for (int i = 0; i < files.Length; i++)
                    {
                        files[i] = CreateRandomFileStream(100 * i + 12);
                        session.RegisterUpload(i + "/test.file", files[i]);
                    }

                    await session.SaveChangesAsync();
                }

                FileSystemSmugglerOperationState result;
                using (new FilesStore { Url = server.Url }.Initialize())
                {
                    // now perform full backup                    
                    result = await smuggler.ExecuteAsync(
                        new RemoteSmugglingSource(new FilesConnectionStringOptions
                        {
                            Url = server.Url,
                            DefaultFileSystem = store.DefaultFileSystem,
                        }),
                        new FileSmugglingDestination(exportFile, false));
                }

                await VerifyDump(store, result.OutputPath, s =>
                {
                    for (int i = 0; i < files.Length; i++)
                    {
                        using (var session = s.OpenAsyncSession())
                        {
                            var file = session.LoadFileAsync(i + "/test.file").Result;
                            var stream = session.DownloadAsync(file).Result;

                            files[i].Position = 0;
                            Assert.Equal(files[i].GetHashAsHex(), stream.GetHashAsHex());
                        }
                    }
                });
            }
        }

        [Fact, Trait("Category", "Smuggler")]
        public async Task MetadataIsPreserved()
        {
            using (var store = NewStore())
            {
                var server = GetServer();
                var outputDirectory = Path.Combine(server.Configuration.Core.DataDirectory, "Export");

                var smuggler = new FileSystemSmuggler(new FileSystemSmugglerOptions());

                FileHeader originalFile;
                using (var session = store.OpenAsyncSession())
                {
                    session.RegisterUpload("test1.file", CreateRandomFileStream(12800));
                    await session.SaveChangesAsync();

                    // content update after a metadata change
                    originalFile = await session.LoadFileAsync("test1.file");
                    originalFile.Metadata["Test"] = new RavenJValue("Value");
                    await session.SaveChangesAsync();
                }

                using (new FilesStore { Url = server.Url }.Initialize())
                {
                    // now perform full backup                    
                    await smuggler.ExecuteAsync(
                        new RemoteSmugglingSource(new FilesConnectionStringOptions
                        {
                            Url = server.Url,
                            DefaultFileSystem = store.DefaultFileSystem,
                        }),
                        new FileSmugglingDestination(outputDirectory, true));
                }

                await VerifyDump(store, outputDirectory, s =>
                {
                    using (var session = s.OpenAsyncSession())
                    {
                        var file = session.LoadFileAsync("test1.file").Result;

                        Assert.Equal(originalFile.CreationDate, file.CreationDate);
                        Assert.Equal(originalFile.Directory, file.Directory);
                        Assert.Equal(originalFile.Extension, file.Extension);
                        Assert.Equal(originalFile.FullPath, file.FullPath);
                        Assert.Equal(originalFile.Name, file.Name);
                        Assert.Equal(originalFile.TotalSize, file.TotalSize);
                        Assert.Equal(originalFile.UploadedSize, file.UploadedSize);
                        Assert.Equal(originalFile.LastModified, file.LastModified);

                        Assert.True(file.Metadata.ContainsKey("Test"));
                    }
                });
            }
        }


        private async Task InitializeUniformFile(IFilesStore store, string name, int size, char content)
        {
            using (var session = store.OpenAsyncSession())
            {
                session.RegisterUpload(name, CreateUniformFileStream(size, content));
                await session.SaveChangesAsync();
            }
        }

        private async Task InitializeRandomFile(IFilesStore store, string name, int size)
        {
            using (var session = store.OpenAsyncSession())
            {
                session.RegisterUpload(name, CreateRandomFileStream(size));
                await session.SaveChangesAsync();
            }            
        }

        private async Task InitializeWithRandomFiles(IFilesStore store, int count, int maxFileSizeInKb = 1024)
        {
            var rnd = new Random();

            var creationTasks = new Task[count];
            for (int i = 0; i < count; i++)
            {
                string name = "file-" + rnd.Next() + ".bin";
                int size = rnd.Next(maxFileSizeInKb / 2, maxFileSizeInKb) * 1024;
                var content = (char)rnd.Next(byte.MaxValue);

                creationTasks[i] = InitializeRandomFile(store, name, size);
            }

            await Task.WhenAll(creationTasks);
        }

        private async Task VerifyDump(FilesStore store, string backupPath, Action<FilesStore> action)
        {
            try
            {
                store.DefaultFileSystem += "-Verify";
                await store.AsyncFilesCommands.Admin.EnsureFileSystemExistsAsync(store.DefaultFileSystem);

                var smuggler = new FileSystemSmuggler(new FileSystemSmugglerOptions());
                              
                await smuggler.ExecuteAsync(
                    new FileSmugglingSource(backupPath),
                    new RemoteSmugglingDestination(new FilesConnectionStringOptions
                    {
                        Url = store.Url,
                        DefaultFileSystem = store.DefaultFileSystem,
                    }));

                action(store);
            }
            catch (Exception e)
            {
                throw e.SimplifyException();
            }
        }
    }
}
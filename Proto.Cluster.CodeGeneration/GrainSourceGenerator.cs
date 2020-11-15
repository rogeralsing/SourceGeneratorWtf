﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Linq;
using Google.Protobuf.Reflection;
using HandlebarsDotNet;
using ProtoBuf.Reflection;

namespace Proto.Cluster.CodeGeneration
{
    [Generator]
    public class GrainSourceGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context) {}

        public void Execute(GeneratorExecutionContext context)
        {
            //just to se if it runs at all.....
            context.AddSource("myGeneratedFile.cs", SourceText.From(@"
//this is just here to see that anything at all is generated...
namespace GeneratedNamespace
{
    public class GeneratedClass
    {
        public static void GeneratedMethod()
        {
            // generated code
        }
    }
}", Encoding.UTF8));
            
            // find anything that matches our files
            var myFiles = context.AdditionalFiles.Where(at => at.Path.EndsWith(".proto")).ToArray();

            foreach (var file in myFiles)
            {

                var content = file!.GetText(context.CancellationToken)!.ToString();

                var csCodeString = GenerateCsCodeForProtoFile(content);

                var sourceText = SourceText.From(csCodeString, Encoding.UTF8);

                context.AddSource($"generated.cs", sourceText);
            }
        }

        public static string GenerateCsCodeForProtoFile(string protoContent)
        {
            var f = new FileDescriptorProto();
            var errors = new List<Error>();
            var t = new StringReader(protoContent);
#pragma warning disable 618
            f.Parse(t, errors, "someFile");
#pragma warning restore 618

            // do some transforms based on the file context
            var ast = new ProtoFile
            {
                PackageName = f.Package,
                CsNamespace = f.Options?.CsharpNamespace ?? f.Package,
                Messages = f
                    .MessageTypes
                    .ToArray()
                    .Select(mt => new ProtoMessage {Name = mt.Name})
                    .ToArray(),
                Services = f
                    .Services
                    .ToArray()
                    .Select(
                        s => new ProtoService
                        {
                            Name = s.Name,
                            Methods = s.Methods.ToArray()
                                .Select(
                                    (m, i) => new ProtoMethod
                                    {
                                        Index = i,
                                        Name = m.Name,
                                        InputName = RemovePackageName(m.InputType),
                                        OutputName = RemovePackageName(m.OutputType),
                                    }
                                )
                                .ToArray()
                        }
                    )
                    .ToArray()
            };
            var f1 = Handlebars.Compile(Template.Code);

            var result = f1(ast);
            var output = result;
            return output;
            
            static string RemovePackageName(ReadOnlySpan<char> type)
            {
                var index = type.LastIndexOf('.');
                return type.Slice(index + 1).ToString();
            }
        }
        
//         public static void Main()
//         {
//             var proto = @"syntax = ""proto3"";
// package messages;
// option csharp_namespace = ""Messages"";
//
// message HelloRequest {}
// message HelloResponse {
//     string Message=1;
// }
//
// service HelloGrain {
// 	rpc SayHello(HelloRequest) returns (HelloResponse) {}
// }
// ";
//
//             var csCode = GenerateCsCodeForProtoFile(proto);
//             
//             Console.WriteLine(csCode);
//
//         }
    }
}
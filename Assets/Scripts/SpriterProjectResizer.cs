using UnityEngine;
using System;
using System.Xml;
using System.IO;
using System.Collections;
using System.Collections.Generic;

public class SpriterProjectResizer
{
    public SpriterProjectResizer()
    {
    }

    public IEnumerator Run(string InputPath, string OutputPath, float NewScale,
        Material bleedMat, Material blurMat, Material resizeMat)
    {
        int lastFileWidth = 0;
        int lastFileHeight = 0;

        var inputDirectory = Path.GetDirectoryName(InputPath);
        var outputDirectory = Path.GetDirectoryName(OutputPath);

        IEnumerator ImageFileHandler(string file, ImageResizer imageResizer)
        {
            file.Replace('/', Path.DirectorySeparatorChar);

            yield return $"Resizing '{file}'";

            string inputFullPath = $"{inputDirectory}{Path.DirectorySeparatorChar}{file}";
            string outputFullPath = $"{outputDirectory}{Path.DirectorySeparatorChar}{file}";

            // Make sure the output directory exists.  Create it if necessary.
            string outputFileDirectory = Path.GetDirectoryName(outputFullPath);

            if (!Directory.Exists(outputFileDirectory))
            {
                Directory.CreateDirectory(outputFileDirectory);
            }

            string errorMsg = null;

            if (!imageResizer.ResizeImage(inputFullPath, outputFullPath, NewScale, ref lastFileWidth, ref lastFileHeight, ref errorMsg))
            {
                yield return errorMsg;
            }
        }

        string GetFileWidthAttribValue(string valueStr, string file) => lastFileWidth.ToString();

        string GetFileHeightAttribValue(string valueStr, string file) => lastFileHeight.ToString();

        string ScaleDoubleValue(string valueStr) => (float.Parse(valueStr) * NewScale).ToString("0.######");

        var replacementsByElement = new Dictionary<string, Dictionary<string, Func<string, string, string>>>
        {
            ["file"] = new Dictionary<string, Func<string, string, string>>
            {
                ["width"] = (oldValue, file) => GetFileWidthAttribValue(oldValue, file),
                ["height"] = (oldValue, file) => GetFileHeightAttribValue(oldValue, file)
            },
            ["obj_info"] = new Dictionary<string, Func<string, string, string>>
            {
                ["w"] = (oldValue, file) => ScaleDoubleValue(oldValue),
                ["h"] = (oldValue, file) => ScaleDoubleValue(oldValue)
            },
            ["bone"] = new Dictionary<string, Func<string, string, string>>
            {
                ["x"] = (oldValue, file) => ScaleDoubleValue(oldValue),
                ["y"] = (oldValue, file) => ScaleDoubleValue(oldValue)
            },
            ["object"] = new Dictionary<string, Func<string, string, string>>
            {
                ["x"] = (oldValue, file) => ScaleDoubleValue(oldValue),
                ["y"] = (oldValue, file) => ScaleDoubleValue(oldValue)
            }
        };

        var imageResizer = new ImageResizer(bleedMat, blurMat, resizeMat);

        IEnumerator task = UpdateSpriterFileAttributes( // And resize images.
            inPath: InputPath,
            outPath: OutputPath,
            fileHandler: (file) => ImageFileHandler(file, imageResizer),
            predicate: (elem, attr, val, file) => replacementsByElement.TryGetValue(elem, out var attrs) && attrs.ContainsKey(attr),
            modifier: (elem, attr, oldVal, file) => replacementsByElement[elem][attr](oldVal, file)
        );

        while (task.MoveNext())
        {
            yield return task.Current;
        }
    }

    private static IEnumerator UpdateSpriterFileAttributes(
        string inPath,
        string outPath,
        Func<string, IEnumerator> fileHandler,
        Func<string, string, string, string, bool> predicate,
        Func<string, string, string, string, string> modifier)
    {
        var readerSettings = new XmlReaderSettings
        {
            IgnoreWhitespace = false,
            IgnoreComments = false,
            IgnoreProcessingInstructions = false
        };

        var writerSettings = new XmlWriterSettings
        {
            Indent = false,
            NewLineHandling = NewLineHandling.Replace,
            OmitXmlDeclaration = false
        };

        using (var reader = XmlReader.Create(inPath, readerSettings))
        using (var writer = XmlWriter.Create(outPath, writerSettings))
        {
            writer.WriteStartDocument(true);

            while (reader.Read())
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:

                        string elementName = reader.LocalName;
                        string currentFileName = elementName == "file" ? reader.GetAttribute("name") : null;

                        if (!string.IsNullOrEmpty(currentFileName))
                        {
                            var process = fileHandler(currentFileName);
                            while (process.MoveNext())
                            {
                                yield return process.Current;
                            }
                        }

                        // write the <tag>
                        writer.WriteStartElement(
                            reader.Prefix,
                            reader.LocalName,
                            reader.NamespaceURI);

                        // copy/patch each attribute
                        if (reader.HasAttributes)
                        {
                            reader.MoveToFirstAttribute();

                            do
                            {
                                var attribName = reader.Name;
                                var attribValue = reader.Value;

                                if (predicate(elementName, attribName, attribValue, currentFileName))
                                {
                                    attribValue = modifier(elementName, attribName, attribValue, currentFileName);
                                }

                                writer.WriteAttributeString(
                                    reader.Prefix,
                                    reader.LocalName,
                                    reader.NamespaceURI,
                                    attribValue);
                            }
                            while (reader.MoveToNextAttribute());

                            reader.MoveToElement();
                        }

                        // automatically close empty elements
                        if (reader.IsEmptyElement)
                        {
                            writer.WriteEndElement();
                        }

                        break;

                    case XmlNodeType.EndElement:
                        writer.WriteFullEndElement();
                        break;

                    case XmlNodeType.Text:
                        writer.WriteString(reader.Value);
                        break;

                    case XmlNodeType.CDATA:
                        writer.WriteCData(reader.Value);
                        break;

                    case XmlNodeType.ProcessingInstruction:
                        writer.WriteProcessingInstruction(
                            reader.Name,
                            reader.Value);
                        break;

                    case XmlNodeType.Comment:
                        writer.WriteComment(reader.Value);
                        break;

                    case XmlNodeType.DocumentType:
                        writer.WriteDocType(
                            reader.Name,
                            reader.GetAttribute("PUBLIC"),
                            reader.GetAttribute("SYSTEM"),
                            reader.Value);
                        break;

                    case XmlNodeType.Whitespace:
                    case XmlNodeType.SignificantWhitespace:
                        // Preserve indent/newlines
                        writer.WriteWhitespace(reader.Value);
                        break;

                    default:
                        // writer.WriteNode(reader, false);
                        break;
                }
            }
        }
    }
}

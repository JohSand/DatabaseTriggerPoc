using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace Poc.Sqltabledependency {
  public sealed partial class SqlDependencyEx {
    public class TableChangedEventArgs : EventArgs {
      private readonly string notificationMessage;

      private const string INSERTED_TAG = "inserted";

      private const string DELETED_TAG = "deleted";

      public TableChangedEventArgs(string notificationMessage) {
        this.notificationMessage = notificationMessage;
      }

      public XElement Data
      {
        get
        {
          if (string.IsNullOrWhiteSpace(notificationMessage)) return null;

          return ReadXDocumentWithInvalidCharacters(notificationMessage);
        }
      }

      public NotificationTypes NotificationType
      {
        get
        {
          if (Data?.Element(INSERTED_TAG) != null && Data?.Element(DELETED_TAG) != null)
            return NotificationTypes.Update;
          else if (Data?.Element(INSERTED_TAG) != null)
            return NotificationTypes.Insert;
          else if (Data?.Element(DELETED_TAG) != null)
            return NotificationTypes.Delete;
          else
            return NotificationTypes.None;
        }
      }

      /// <summary>
      /// Converts an xml string into XElement with no invalid characters check.
      /// https://paulselles.wordpress.com/2013/07/03/parsing-xml-with-invalid-characters-in-c-2/
      /// </summary>
      /// <param name="xml">The input string.</param>
      /// <returns>The result XElement.</returns>
      private static XElement ReadXDocumentWithInvalidCharacters(string xml) {
        XDocument xDocument = null;

        XmlReaderSettings xmlReaderSettings = new XmlReaderSettings { CheckCharacters = false };

        using (var stream = new StringReader(xml))
        using (XmlReader xmlReader = XmlReader.Create(stream, xmlReaderSettings)) {
          // Load our XDocument
          xmlReader.MoveToContent();
          xDocument = XDocument.Load(xmlReader);
        }

        return xDocument.Root;
      }
    }
  }
}

﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace JSONDB.JQLEditor {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "4.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class AppResources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal AppResources() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("JSONDB.JQLEditor.AppResources", typeof(AppResources).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &lt;?xml version=&quot;1.0&quot; encoding=&quot;utf-8&quot; ?&gt;
        ///
        ///&lt;Syntax name=&quot;JQL&quot;&gt;
        ///  &lt;HighlightLineRule name=&quot;Comment&quot;&gt;
        ///    &lt;LineStart&gt;//&lt;/LineStart&gt;
        ///    &lt;IgnoreCase&gt;false&lt;/IgnoreCase&gt;
        ///    &lt;Foreground&gt;#8E908C&lt;/Foreground&gt;
        ///    &lt;FontWeight&gt;Normal&lt;/FontWeight&gt;
        ///    &lt;FontStyle&gt;Italic&lt;/FontStyle&gt;
        ///    &lt;TextDecoration&gt;Normal&lt;/TextDecoration&gt;
        ///  &lt;/HighlightLineRule&gt;
        ///
        ///  &lt;AdvancedHighlightRule name=&quot;Identifier&quot;&gt;
        ///    &lt;Expression&gt;\b([`]?\w+[`]?)\b&lt;/Expression&gt;
        ///    &lt;HighlightExpressionIndex&gt;1&lt;/HighlightExpressionIndex&gt;
        ///    &lt;IgnoreCase&gt;true&lt;/Ig [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string JQLSyntax {
            get {
                return ResourceManager.GetString("JQLSyntax", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized resource of type System.Drawing.Bitmap.
        /// </summary>
        internal static System.Drawing.Bitmap MessageWindowError {
            get {
                object obj = ResourceManager.GetObject("MessageWindowError", resourceCulture);
                return ((System.Drawing.Bitmap)(obj));
            }
        }
        
        /// <summary>
        ///   Looks up a localized resource of type System.Drawing.Bitmap.
        /// </summary>
        internal static System.Drawing.Bitmap MessageWindowInformation {
            get {
                object obj = ResourceManager.GetObject("MessageWindowInformation", resourceCulture);
                return ((System.Drawing.Bitmap)(obj));
            }
        }
        
        /// <summary>
        ///   Looks up a localized resource of type System.Drawing.Bitmap.
        /// </summary>
        internal static System.Drawing.Bitmap MessageWindowSuccess {
            get {
                object obj = ResourceManager.GetObject("MessageWindowSuccess", resourceCulture);
                return ((System.Drawing.Bitmap)(obj));
            }
        }
        
        /// <summary>
        ///   Looks up a localized resource of type System.Drawing.Bitmap.
        /// </summary>
        internal static System.Drawing.Bitmap MessageWindowWarning {
            get {
                object obj = ResourceManager.GetObject("MessageWindowWarning", resourceCulture);
                return ((System.Drawing.Bitmap)(obj));
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &lt;?xml version=&quot;1.0&quot; encoding=&quot;UTF-8&quot;?&gt;
        ///&lt;xs:schema xmlns:xs=&quot;http://www.w3.org/2001/XMLSchema&quot; elementFormDefault=&quot;qualified&quot;&gt;
        ///  &lt;xs:element name=&quot;Syntax&quot;&gt;
        ///    &lt;xs:complexType&gt;
        ///      &lt;xs:sequence&gt;
        ///        &lt;xs:element ref=&quot;HighlightWordsRule&quot; minOccurs=&quot;0&quot; maxOccurs=&quot;unbounded&quot;/&gt;
        ///        &lt;xs:element ref=&quot;HighlightLineRule&quot; minOccurs=&quot;0&quot; maxOccurs=&quot;unbounded&quot;/&gt;
        ///        &lt;xs:element ref=&quot;AdvancedHighlightRule&quot; minOccurs=&quot;0&quot; maxOccurs=&quot;unbounded&quot;/&gt;
        ///      &lt;/xs:sequence&gt;
        ///      &lt;xs:attribute name=&quot;name&quot; use [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string SyntaxSchema {
            get {
                return ResourceManager.GetString("SyntaxSchema", resourceCulture);
            }
        }
    }
}

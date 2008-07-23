// 
// XmlClosingTagState.cs
// 
// Author:
//   Michael Hutchinson <mhutchinson@novell.com>
// 
// Copyright (C) 2008 Novell, Inc (http://www.novell.com)
// 
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace MonoDevelop.Xml.StateEngine
{
	
	
	public class XmlClosingTagState : State
	{
		XmlNameState NameState;
		XmlMalformedTagState MalformedTagState;
		
		public XmlClosingTagState ()
			: this (new XmlNameState ()) {}
		
		public XmlClosingTagState (XmlNameState nameState)
			: this (nameState, new XmlMalformedTagState ()) {}
		
		public XmlClosingTagState (XmlNameState nameState, XmlMalformedTagState malformedTagState)
		{
			NameState = nameState;
			MalformedTagState = malformedTagState;
			Adopt (NameState);
			Adopt (MalformedTagState);
		}

		public override State PushChar (char c, IParseContext context, ref bool reject)
		{
			XClosingTag ct = context.Nodes.Peek () as XClosingTag;
			
			if (ct == null) {
				Debug.Assert (context.CurrentStateLength == 1,
					"IncompleteNode must not be an XClosingTag when CurrentStateLength is 1");
				Debug.Assert (c == '/', "First character pushed to a XmlClosingTagState must be '/'");
				Debug.Assert (context.Nodes.Peek () is XElement);
				
				ct = new XClosingTag (context.Position - 1);
				context.Nodes.Push (ct);
			}
			
			//if tag closed
			if (c == '>') {
				context.Nodes.Pop ();
				
				if (ct.IsNamed) {
					ct.End (context.Position);
					
					// walk up tree of parents looking for matching tag
					int popCount = 0;
					bool found = false;
					foreach (XObject node in context.Nodes) {
						popCount++;
						XElement element = node as XElement;
						if (element != null && element.Name == ct.Name) {
							found = true;
							break;
						}
					}
					if (!found)
						popCount = 0;
					
					//clear the stack of intermediate unclosed tags
					while (popCount > 1) {
						XElement el = context.Nodes.Pop () as XElement;
						if (el != null)
							context.LogError (string.Format (
								"Unclosed tag '{0}' at '{1}'.", el.Name.FullName, el.Position));
						popCount--;
					}
					
					//close the start tag, if we found it
					if (context.BuildTree) {
						if (popCount > 0) {
							XElement element = (XElement) context.Nodes.Pop ();
							if (context.BuildTree)
								element.Close (ct);
						} else {
							context.LogError (
								"Closing tag '" + ct.Name.FullName + "' does not match any currently open tag.");
						}
					} else {
						context.Nodes.Pop ();
					}
					
				} else {
					context.LogError ("Closing tag ended prematurely.");
				}
				return Parent;
			}
			
			if (c == '<') {
				context.LogError ("Unexpected '<' in tag.");
				reject = true;
				return Parent;
			}
			
			if (!ct.IsNamed && (char.IsLetter (c) || c == '_')) {
				reject = true;
				return NameState;
			}
			
			reject = true;
			context.LogError ("Unexpected character '" + c + "' in closing tag.");
			return MalformedTagState;
		}
	}
}

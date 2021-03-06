﻿//
// OrganizeImportsCommandHandler.cs
//
// Author:
//       Mike Krüger <mkrueger@xamarin.com>
//
// Copyright (c) 2016 Xamarin Inc. (http://xamarin.com)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;
using MonoDevelop.Components.Commands;
using ICSharpCode.NRefactory6.CSharp.ExtractMethod;
using MonoDevelop.Ide;
using ICSharpCode.NRefactory6.CSharp;
using System.Threading;
using Microsoft.CodeAnalysis.Text;
using System.Linq;
using MonoDevelop.Refactoring;
using MonoDevelop.Core;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using ICSharpCode.NRefactory6.CSharp.Features.OrganizeImports;
using System.Collections.Generic;
using ICSharpCode.NRefactory6.CSharp.Features.RemoveUnnecessaryImports;

namespace MonoDevelop.CSharp.Refactoring
{
	internal static partial class IWorkspaceExtensions
	{
		/// <summary>
		/// Update the workspace so that the document with the Id of <paramref name="newDocument"/>
		/// has the text of newDocument.  If the document is open, then this method will determine a
		/// minimal set of changes to apply to the document.
		/// </summary>
		internal static void ApplyDocumentChanges (this Workspace workspace, Document newDocument, CancellationToken cancellationToken)
		{
			var oldSolution = workspace.CurrentSolution;
			var oldDocument = oldSolution.GetDocument (newDocument.Id);
			var changes = newDocument.GetTextChangesAsync (oldDocument, cancellationToken).WaitAndGetResult (cancellationToken);
			var newSolution = oldSolution.UpdateDocument (newDocument.Id, changes, cancellationToken);
			workspace.TryApplyChanges (newSolution);
		}


		internal static Solution UpdateDocument(this Solution solution, DocumentId id, IEnumerable<TextChange> textChanges, CancellationToken cancellationToken)
		{
			var oldDocument = solution.GetDocument(id);
			var oldText = oldDocument.GetTextAsync(cancellationToken).WaitAndGetResult(cancellationToken);
			var newText = oldText.WithChanges(textChanges);
			return solution.WithDocumentText(id, newText, PreservationMode.PreserveIdentity);
		}
	}

	class RemoveUnusedImportsCommandHandler : CommandHandler
	{
		internal static readonly CSharpRemoveUnnecessaryImportsService service = new CSharpRemoveUnnecessaryImportsService();

		public async static Task Run (MonoDevelop.Ide.Gui.Document doc)
		{
			var ad = doc.AnalysisDocument;
			if (ad == null)
				return;
			try {

				var model = await ad.GetSemanticModelAsync (default (CancellationToken));
				var root = model.SyntaxTree.GetRoot (default (CancellationToken));
				var newDocument = service.RemoveUnnecessaryImports(ad, model, root, default (CancellationToken));
				ad.Project.Solution.Workspace.ApplyDocumentChanges(newDocument, CancellationToken.None);

			} catch (Exception e) {
				LoggingService.LogError ("Error while removing unused usings", e);
			}
		}

		protected async override void Run ()
		{
			var doc = IdeApp.Workbench.ActiveDocument;
			if (doc == null)
				return;
			await Run (doc);
		}
	}

	class OrganizeImportsCommandHandler : CommandHandler
	{
		internal static readonly CSharpOrganizeImportsService service = new CSharpOrganizeImportsService ();

		public async static Task Run (MonoDevelop.Ide.Gui.Document doc)
		{
			var ad = doc.AnalysisDocument;
			if (ad == null)
				return;
			try {
				Document newDocument = await SortUsingsAsync (ad, default (CancellationToken));
				ad.Project.Solution.Workspace.ApplyDocumentChanges (newDocument, CancellationToken.None);

			} catch (Exception e) {
				LoggingService.LogError ("Error while sotring usings", e);
			}
		}

		internal static async Task<Document> SortUsingsAsync (Document ad, CancellationToken token)
		{
			var policy = IdeApp.Workbench.ActiveDocument.GetFormattingPolicy ();
			Console.WriteLine (policy.PlaceSystemDirectiveFirst);
			return await service.OrganizeImportsAsync (ad, policy != null ? policy.PlaceSystemDirectiveFirst : true, token);
		}

		protected async override void Run ()
		{
			var doc = IdeApp.Workbench.ActiveDocument;
			if (doc == null)
				return;
			await Run (doc);
		}
	}

	class SortAndRemoveImportsCommandHandler : CommandHandler
	{
		public async static Task Run (MonoDevelop.Ide.Gui.Document doc)
		{
			var ad = doc.AnalysisDocument;
			if (ad == null)
				return;
			try {
				Document newDocument = await SortAndRemoveAsync (ad, default (CancellationToken));
				ad.Project.Solution.Workspace.ApplyDocumentChanges (newDocument, CancellationToken.None);
			} catch (Exception e) {
				LoggingService.LogError ("Error while removing unused usings", e);
			}
		}

		internal static async Task<Document> SortAndRemoveAsync (Document ad, CancellationToken token)
		{
			var model = await ad.GetSemanticModelAsync (token);
			var root = model.SyntaxTree.GetRoot (token);
			var newDocument = RemoveUnusedImportsCommandHandler.service.RemoveUnnecessaryImports (ad, model, root, token);
			return await OrganizeImportsCommandHandler.SortUsingsAsync (newDocument, token);
		}

		protected async override void Run ()
		{
			var doc = IdeApp.Workbench.ActiveDocument;
			if (doc == null)
				return;
			await Run (doc);
		}
	}
}


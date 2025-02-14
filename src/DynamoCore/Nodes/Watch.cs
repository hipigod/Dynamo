﻿using System;
using System.Collections.Generic;

using Dynamo.Controls;
using Dynamo.Interfaces;
using Dynamo.Models;
using Dynamo.ViewModels;

using ProtoCore.AST.AssociativeAST;

using VMDataBridge;

namespace Dynamo.Nodes
{
    public interface WatchHandler
    {
        bool AcceptsValue(object o);
        void ProcessNode(object value, WatchViewModel node, bool showRawData);
    }

    [NodeName("Watch")]
    [NodeCategory(BuiltinNodeCategories.CORE_VIEW)]
    [NodeDescription("Visualize the output of node. ")]
    [NodeSearchTags("print", "output", "display")]
    [IsDesignScriptCompatible]
    public partial class Watch : NodeModel
    {
        #region private members

        private DynamoViewModel dynamoViewModel;
        private WatchTree watchTree;
        private WatchViewModel root;

        private IdentifierNode astBeingWatched;


        #endregion

        #region public properties

        public new object CachedValue { get; private set; }

        /// <summary>
        /// The root node of the watch's tree.
        /// </summary>
        public WatchViewModel Root
        {
            get { return root; }
            set
            {
                root = value;
                RaisePropertyChanged("Root");
            }
        }

        #endregion

        #region events

        /// <summary>
        /// This event is handled by the UI and allows for 
        /// rapid regeneration of Watch content.
        /// </summary>
        public event EventHandler RequestBindingUnhook;
        
        /// <summary>
        /// After the Watch content has been regenerated, this 
        /// event is triggered to reestablish the bindings.
        /// </summary>
        public event EventHandler RequestBindingRehook;

        #endregion

        public Watch(WorkspaceModel ws)
            : base(ws)
        {
            InPortData.Add(new PortData("", "Node to evaluate."));
            OutPortData.Add(new PortData("", "Watch contents."));

            RegisterAllPorts();

            ArgumentLacing = LacingStrategy.Disabled;

            foreach (PortModel p in InPorts)
            {
                p.PortConnected += InputPortConnected;
            }
        }

        private void EvaluationCompleted(object o)
        {
            CachedValue = o;
            DispatchOnUIThread(
                delegate
                {
                    //unhook the binding
                    OnRequestBindingUnhook(EventArgs.Empty);

                    Root.Children.Clear();
                    Root.Children.Add(GetWatchNode());

                    //rehook the binding
                    OnRequestBindingRehook(EventArgs.Empty);
                }
            );
        }

        public override void Destroy()
        {
            base.Destroy();
            DataBridge.Instance.UnregisterCallback(GUID.ToString());
        }

        /// <summary>
        ///     Callback for port connection. Handles clearing the watch.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void InputPortConnected(object sender, EventArgs e)
        {
            Tuple<int, NodeModel> input;
            if (TryGetInput(InPorts.IndexOf(sender as PortModel), out input))
            {
                var oldId = astBeingWatched;
                astBeingWatched = input.Item2.GetAstIdentifierForOutputIndex(input.Item1);
                if (oldId != null && astBeingWatched.Value != oldId.Value)
                {
                    CachedValue = null;
                    if (Root != null)
                        Root.Children.Clear();
                }
            }
        }
        
        protected virtual void OnRequestBindingUnhook(EventArgs e)
        {
            if (RequestBindingUnhook != null)
                RequestBindingUnhook(this, e);
        }

        protected virtual void OnRequestBindingRehook(EventArgs e)
        {
            if (RequestBindingRehook != null)
                RequestBindingRehook(this, e);
        }

        public override IdentifierNode GetAstIdentifierForOutputIndex(int outputIndex)
        {
            return outputIndex == 0
                ? AstIdentifierForPreview
                : base.GetAstIdentifierForOutputIndex(outputIndex);
        }

        protected override void OnBuilt()
        {
            base.OnBuilt();
            DataBridge.Instance.RegisterCallback(GUID.ToString(), EvaluationCompleted);
        }

        public override IEnumerable<AssociativeNode> BuildOutputAst(
            List<AssociativeNode> inputAstNodes)
        {
            if (IsPartiallyApplied)
            {
                return new[]
                {
                    AstFactory.BuildAssignment(
                        GetAstIdentifierForOutputIndex(0),
                        AstFactory.BuildFunctionObject(
                            new IdentifierListNode
                            {
                                LeftNode = AstFactory.BuildIdentifier("DataBridge"),
                                RightNode = AstFactory.BuildIdentifier("BridgeData")
                            },
                            2,
                            new[] { 0 },
                            new List<AssociativeNode>
                            {
                                AstFactory.BuildStringNode(GUID.ToString()),
                                AstFactory.BuildNullNode()
                            }))
                };
            }

            var resultAst = new[]
            {
                AstFactory.BuildAssignment(
                    AstFactory.BuildIdentifier(AstIdentifierBase + "_dummy"),
                    DataBridge.GenerateBridgeDataAst(GUID.ToString(), inputAstNodes[0])),
                AstFactory.BuildAssignment(GetAstIdentifierForOutputIndex(0), inputAstNodes[0])
            };

            return resultAst;
        }

        #region Watch Node creation for AST node

        /// <summary>
        /// This method returns a WatchNode for it's preview AST node.
        /// This method gets called on ui thread when "IsUpdated" property
        /// change is notified. This method is responsible for populating the 
        /// watch node with evaluated value of the input. Gets the MirrorData
        /// for the input/preview AST and then processes the mirror data to
        /// render the watch content properly.
        /// </summary>
        /// <returns>WatchNode</returns>
        internal WatchViewModel GetWatchNode()
        {
            var inputVar = IsPartiallyApplied
                ? AstIdentifierForPreview.Name
                : InPorts[0].Connectors[0].Start.Owner.AstIdentifierForPreview.Name;
            
            return Root != null
                ? dynamoViewModel.WatchHandler.GenerateWatchViewModelForData(CachedValue, inputVar, Root.ShowRawData)
                : dynamoViewModel.WatchHandler.GenerateWatchViewModelForData(CachedValue, inputVar);
        }

        public override void UpdateRenderPackage(int maxTessDivs)
        {
            //do nothing
            //a watch should not draw its outputs
        }

        #endregion
    }
}

﻿using System;

namespace Dynamo.Models
{
    public delegate void CleanupHandler(DynamoModel dynamoModel);
    public delegate void NodeHandler(NodeModel node);
    public delegate void ConnectorHandler(ConnectorModel connector);
    public delegate void WorkspaceHandler(WorkspaceModel model);
    public delegate void ActionHandler(Action action);
}

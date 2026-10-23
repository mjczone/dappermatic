// Relay's view of DapperMatic's schema: a connection paged by cursor, a fragment refetched through node(id:), an insert
// appending its edges to the connection, a live query. relay-compiler validates each against the schema's rules.
declare function graphql(strings: TemplateStringsArray): unknown;

export const page = graphql`
  query OrdersPageQuery($first: Int!, $after: String) {
    ...Orders_list @arguments(first: $first, after: $after)
  }
`;

export const list = graphql`
  fragment Orders_list on Query @argumentDefinitions(first: { type: "Int!" }, after: { type: "String" }) {
    orders_connection(first: $first, after: $after, orderBy: [{ id: ASC }]) @connection(key: "Orders_orders_connection") {
      __id
      totalCount
      edges {
        node {
          ...Orders_order
        }
      }
    }
  }
`;

export const order = graphql`
  fragment Orders_order on orders @refetchable(queryName: "OrdersOrderRefetchQuery") {
    nodeId
    id
    total
    status
    customer {
      nodeId
      name
    }
  }
`;

export const add = graphql`
  mutation OrdersAddMutation($rows: [orders_insert!]!, $connections: [ID!]!) {
    insert_orders(rows: $rows) {
      affected
      edges @appendEdge(connections: $connections) {
        cursor
        node {
          ...Orders_order
        }
      }
    }
  }
`;

export const live = graphql`
  subscription OrdersLiveSubscription {
    orders(orderBy: [{ id: ASC }]) {
      nodeId
      status
    }
  }
`;

export const node = graphql`
  query OrdersNodeQuery($id: ID!) {
    node(id: $id) {
      nodeId
      ... on customers {
        name
      }
    }
  }
`;

// Topology islands for Squad app pages.
// Visual grammar follows diagram-design's editorial "architecture" and "deployment" types:
// 1px rules, no shadows, small mono kind labels, generous space, and the accent reserved for
// the one or two nodes a reader should look at first (the entry point and the data store).
import { StrictMode, useEffect, useMemo } from 'react';
import { createRoot } from 'react-dom/client';
import { ReactFlow, Handle, Position, MarkerType } from '@xyflow/react';
import '@xyflow/react/dist/base.css';
import './topology.css';

const KIND_LABELS = {
  frontend: 'Front end', project: 'Project', 'database-server': 'Database server', database: 'Database',
  gateway: 'Entry', namespace: 'Namespace', service: 'Service', deployment: 'Deployment',
  statefulset: 'StatefulSet', volume: 'Volume', config: 'ConfigMap', secret: 'Secret'
};
const STATUS_LABELS = { ready: 'Ready', starting: 'Starting', failing: 'Failing', stopped: 'Stopped' };
/** One short line for the node: the reason code when failing, otherwise the ready count. */
function shortStatus(status) {
  const parts = status.detail.split(' · ');
  const reason = status.state === 'failing' ? parts.find(part => /^[A-Z][A-Za-z]+$/.test(part)) : null;
  return `${STATUS_LABELS[status.state] ?? status.state} · ${reason ?? parts[0].replace(' ready', '')}`;
}
const ACCENT = new Set(['frontend', 'database-server', 'gateway', 'statefulset']);
const ATTACHMENT = new Set(['config', 'secret']);

const SIZE = { full: { w: 168, h: 64, gapX: 112, gapY: 24, pad: 28 }, compact: { w: 96, h: 30, gapX: 22, gapY: 8, pad: 12 } };

function TopologyNode({ data }) {
  const { node, compact, vertical, status } = data;
  return (
    <div className={`tn tn-${node.kind}${ACCENT.has(node.kind) ? ' tn-accent' : ''}${ATTACHMENT.has(node.kind) ? ' tn-attachment' : ''}${status ? ` tn-live tn-live-${status.state}` : ''}`}>
      <Handle type="target" position={vertical ? Position.Top : Position.Left} isConnectable={false} />
      {!compact && <span className="tn-kind">{KIND_LABELS[node.kind] ?? node.kind}</span>}
      <span className="tn-label" title={node.title}>{status && <span className="tn-dot" aria-hidden="true" />}{node.label}</span>
      {!compact && node.detail && <span className="tn-detail">{node.detail}</span>}
      {!compact && status && <span className="tn-status" title={status.detail}>{shortStatus(status)}</span>}
      <Handle type="source" position={vertical ? Position.Bottom : Position.Right} isConnectable={false} />
    </div>
  );
}

function NamespaceNode({ data }) {
  return (
    <div className="tn-namespace">
      {!data.compact && <span className="tn-kind">Namespace</span>}
      <span className="tn-namespace-label">{data.node.label}</span>
    </div>
  );
}

const nodeTypes = { topology: TopologyNode, namespace: NamespaceNode };

/** Longest-path columns, left to right; nodes sharing a column stack into rows. */
function layerCells(nodes, edges) {
  const rank = new Map(nodes.map(node => [node.id, 0]));
  for (let pass = 0; pass < nodes.length; pass++)
    for (const edge of edges)
      if (rank.has(edge.from) && rank.has(edge.to)) rank.set(edge.to, Math.max(rank.get(edge.to), rank.get(edge.from) + 1));
  const used = new Map();
  return new Map(nodes.map(node => {
    const col = rank.get(node.id);
    const row = used.get(col) ?? 0;
    used.set(col, row + 1);
    return [node.id, { col, row }];
  }));
}

/** Deployment grammar: the entry outside the namespace, then one lane per component (Service, workload, volume). */
function clusterCells(nodes, edges) {
  const column = { gateway: 0, service: 1, deployment: 2, statefulset: 2, volume: 3 };
  const cells = new Map();
  const target = (from, label) => edges.find(edge => edge.from === from && edge.label === label)?.to;
  let lane = 0;
  for (const service of nodes.filter(node => node.kind === 'service')) {
    cells.set(service.id, { col: 1, row: lane });
    const workload = target(service.id, 'selects');
    if (workload && !cells.has(workload)) {
      cells.set(workload, { col: 2, row: lane });
      const volume = target(workload, 'mounts');
      if (volume) cells.set(volume, { col: 3, row: lane });
    }
    lane++;
  }
  for (const node of nodes.filter(node => !cells.has(node.id)))
    cells.set(node.id, { col: column[node.kind] ?? 2, row: node.kind === 'gateway' ? 0 : lane++ });
  return cells;
}

/** Grid layout. Attachments (config, secrets) sit under the workload that uses them. */
function layout(view, compact, vertical = false, live = null, available = 0) {
  const base = compact ? SIZE.compact : SIZE.full;
  // Live status adds a line to each node.
  const s = live && !compact ? { ...base, w: base.w + 24, h: base.h + 16 } : base;
  const nodes = view.nodes.filter(node => !(compact && ATTACHMENT.has(node.kind)));
  const ids = new Set(nodes.map(node => node.id));
  const edges = view.edges.filter(edge => ids.has(edge.from) && ids.has(edge.to));
  const group = nodes.find(node => node.kind === 'namespace');
  const members = nodes.filter(node => node !== group);

  const attachments = new Map();
  const primary = [];
  for (const node of members) {
    const owner = ATTACHMENT.has(node.kind) ? edges.find(edge => edge.to === node.id)?.from : null;
    if (owner) attachments.set(owner, [...(attachments.get(owner) ?? []), node]);
    else primary.push(node);
  }
  const cell = group ? clusterCells(primary, edges) : layerCells(primary, edges);
  const gapX = group && !compact ? s.gapX * 0.6 : s.gapX;

  // Rows are as tall as their tallest stack (a node plus the attachments tucked under it).
  const attachmentHeight = s.h * 0.62;
  // A workload's ConfigMap and Secret sit side by side in one row under it.
  const stack = id => s.h + (attachments.get(id)?.length ? attachmentHeight + 6 : 0);
  const attachmentWidth = count => (s.w - 12 - 6 * (count - 1)) / Math.max(count, 1);
  const rowHeight = new Map();
  for (const node of primary) {
    const { row } = cell.get(node.id);
    rowHeight.set(row, Math.max(rowHeight.get(row) ?? 0, stack(node.id)));
  }
  const rowTop = new Map();
  let top = 0;
  for (const row of [...rowHeight.keys()].sort((a, b) => a - b)) { rowTop.set(row, top); top += rowHeight.get(row) + s.gapY; }

  const placed = new Map();
  if (vertical) {
    // Narrow containers read top to bottom: columns become tiers, lanes sit side by side,
    // and attachments move to the right of their owner.
    const attachmentWidth = s.w - 24;
    const hasAttachments = attachments.size > 0;
    const laneWidth = s.w + (hasAttachments ? attachmentWidth + 8 : 0) + 32;
    const tierHeight = new Map();
    for (const node of primary) {
      const { col } = cell.get(node.id);
      const depth = Math.max(s.h, (attachments.get(node.id)?.length ?? 0) * (attachmentHeight + 6));
      tierHeight.set(col, Math.max(tierHeight.get(col) ?? 0, depth));
    }
    const tierTop = new Map();
    let y = 0;
    // Leave room for the namespace header between the entry tier and the first tier inside it.
    for (const col of [...tierHeight.keys()].sort((a, b) => a - b)) {
      tierTop.set(col, y);
      y += tierHeight.get(col) + s.gapY * 2.5 + (group && col === 0 ? s.pad + 44 : 0);
    }
    // Lanes wrap into as many rows as the width needs, so a big app stays readable on a phone.
    const perRow = Math.max(1, Math.floor((available || Infinity) / laneWidth));
    const firstTier = Math.min(...[...tierTop.keys()].filter(col => col > 0), Infinity);
    const block = y - (Number.isFinite(firstTier) ? tierTop.get(firstTier) : 0) + s.gapY;
    for (const node of primary) {
      const { col, row } = cell.get(node.id);
      const wrap = col === 0 && group ? 0 : Math.floor(row / perRow);
      const x = (col === 0 && group ? row : row % perRow) * laneWidth;
      placed.set(node.id, { x, y: tierTop.get(col) + wrap * block });
      for (const [index, attachment] of (attachments.get(node.id) ?? []).entries())
        placed.set(attachment.id, { x: x + s.w + 8, y: tierTop.get(col) + wrap * block + index * (attachmentHeight + 6), small: true });
    }
  } else {
    for (const node of primary) {
      const { col, row } = cell.get(node.id);
      const x = col * (s.w + gapX);
      const y = rowTop.get(row);
      placed.set(node.id, { x, y });
      const own = attachments.get(node.id) ?? [];
      for (const [index, attachment] of own.entries())
        placed.set(attachment.id, { x: x + 12 + index * (attachmentWidth(own.length) + 6), y: y + s.h + 6, small: true, width: attachmentWidth(own.length) });
    }
  }

  const flowNodes = [];
  if (group) {
    const inside = members.filter(node => node.parent === group.id && placed.has(node.id));
    const minX = Math.min(...inside.map(node => placed.get(node.id).x));
    const minY = Math.min(...inside.map(node => placed.get(node.id).y));
    const maxX = Math.max(...inside.map(node => placed.get(node.id).x + (placed.get(node.id).width ?? (placed.get(node.id).small ? s.w - 24 : s.w))));
    const maxY = Math.max(...inside.map(node => placed.get(node.id).y + (placed.get(node.id).small ? attachmentHeight : s.h)));
    const top = compact ? s.pad : s.pad + (vertical ? 44 : 18);
    flowNodes.push({
      id: group.id, type: 'namespace', data: { node: group, compact },
      position: { x: minX - s.pad, y: minY - top },
      style: { width: maxX - minX + s.pad * 2, height: maxY - minY + top + s.pad },
      draggable: false, selectable: false
    });
    for (const node of inside) {
      const p = placed.get(node.id);
      p.x -= minX - s.pad;
      p.y += top - minY;
    }
  }
  for (const node of members) {
    const p = placed.get(node.id);
    if (!p) continue;
    const insideGroup = group && node.parent === group.id;
    // Under its owner an attachment's name prefix is redundant ("api-config" under api-deployment reads "config").
    const owner = p.small ? [...attachments.entries()].find(([, list]) => list.includes(node))?.[0] : null;
    const stem = owner ? (members.find(item => item.id === owner)?.label ?? '').replace(/-(deployment|statefulset)$/, '') : '';
    // Aspire prefixes volume claims with "apphost-<hash>-"; the rest names the data ("db-data").
    const volume = node.kind === 'volume' ? /^apphost-[0-9a-f]{6,}-(.+)$/.exec(node.label)?.[1] : null;
    const shown = stem && node.label.startsWith(stem + '-') ? { ...node, label: node.label.slice(stem.length + 1), title: node.label }
      : volume ? { ...node, label: volume, title: node.label } : node;
    flowNodes.push({
      id: node.id, type: 'topology', data: { node: shown, compact, vertical, status: live?.[node.id] ?? null },
      position: { x: p.x, y: p.y },
      style: { width: p.width ?? (p.small ? s.w - 24 : s.w), height: p.small ? attachmentHeight : s.h },
      ...(insideGroup ? { parentId: group.id, extent: 'parent' } : {}),
      draggable: false, selectable: false
    });
  }
  // Attachments sit directly under their owner, so drawing those links would only add noise.
  const attached = new Set([...attachments.values()].flat().map(node => node.id));
  const flowEdges = edges.filter(edge => edge.from !== group?.id && edge.to !== group?.id && !attached.has(edge.to)).map((edge, index) => ({
    id: `e${index}`, source: edge.from, target: edge.to, type: 'smoothstep',
    label: compact ? undefined : edge.label,
    className: edge.style === 'dashed' ? 'te-dashed' : 'te-solid',
    markerEnd: { type: MarkerType.ArrowClosed, width: 12, height: 12 },
    focusable: false, selectable: false
  }));
  // Content size at zoom 1 (top-level nodes only; children are positioned inside the namespace).
  const width = Math.max(0, ...flowNodes.filter(node => !node.parentId).map(node => node.position.x + node.style.width)) -
    Math.min(0, ...flowNodes.filter(node => !node.parentId).map(node => node.position.x));
  const height = Math.max(0, ...flowNodes.filter(node => !node.parentId).map(node => node.position.y + node.style.height)) -
    Math.min(0, ...flowNodes.filter(node => !node.parentId).map(node => node.position.y));
  return { flowNodes, flowEdges, width, height };
}

function Diagram({ data, view, compact, vertical, onSize, fit = 0, available = 0 }) {
  const selected = data?.[view] ?? { nodes: [], edges: [] };
  const live = data?.live ?? null;
  const { flowNodes, flowEdges, width, height } = useMemo(() => layout(selected, compact, vertical, live, available), [selected, compact, vertical, live, available]);
  useEffect(() => { onSize?.(width, height); }, [onSize, width, height]);
  return (
    <ReactFlow
      key={`${view}-${vertical ? 'vertical' : 'horizontal'}-${fit}`}
      nodes={flowNodes}
      edges={flowEdges}
      nodeTypes={nodeTypes}
      fitView
      fitViewOptions={{ padding: compact ? 0.08 : 0.12 }}
      minZoom={0.2}
      maxZoom={1.5}
      nodesDraggable={false}
      nodesConnectable={false}
      nodesFocusable={false}
      edgesFocusable={false}
      elementsSelectable={false}
      zoomOnScroll={false}
      zoomOnDoubleClick={false}
      zoomOnPinch={!compact}
      panOnDrag={!compact}
      preventScrolling={false}
      proOptions={{ hideAttribution: true }}
    />
  );
}

/** Plain-text equivalent of the diagram for screen readers. */
function describe(data, view) {
  const selected = data?.[view] ?? { nodes: [], edges: [] };
  const names = new Map(selected.nodes.map(node => [node.id, node.label]));
  const live = data?.live ?? {};
  const items = selected.nodes.map(node => `${node.label}: ${KIND_LABELS[node.kind] ?? node.kind}${node.detail ? `, ${node.detail}` : ''}` +
    (live[node.id] ? `. ${STATUS_LABELS[live[node.id].state] ?? live[node.id].state}, ${live[node.id].detail}` : ''));
  const links = selected.edges.map(edge => `${names.get(edge.from) ?? edge.from} to ${names.get(edge.to) ?? edge.to}${edge.label ? ` (${edge.label})` : ''}`);
  return { items, links };
}

function Island({ data, view, compact, vertical, onSize, fit, available }) {
  // Compact islands are decorative thumbnails; the page labels them, so they carry no description.
  if (compact) return <div className="topology-canvas" aria-hidden="true"><Diagram data={data} view={view} compact /></div>;
  const { items, links } = describe(data, view);
  return (
    <>
      <div className="topology-canvas" aria-hidden="true"><Diagram data={data} view={view} compact={compact} vertical={vertical} onSize={onSize} fit={fit} available={available} /></div>
      <div className="topology-description">
        <p>{view === 'aspire' ? 'Aspire resources' : 'Cluster objects'}</p>
        <ul>{items.map(item => <li key={item}>{item}</li>)}</ul>
        {links.length > 0 && <><p>Connections</p><ul>{links.map(link => <li key={link}>{link}</li>)}</ul></>}
      </div>
    </>
  );
}

/**
 * Mounts a read-only topology diagram into `element`.
 * Returns { update(data, options), unmount() }. Options: { view: 'aspire' | 'cluster', compact }.
 */
export function mountTopology(element, data, options = {}) {
  const root = createRoot(element);
  element.classList.add('topology-island');
  let current = { data, view: options.view ?? 'aspire', compact: Boolean(options.compact) };
  // Full diagrams switch to a top-to-bottom layout when their container is narrow (phones).
  const narrow = () => !current.compact && element.clientWidth > 0 && element.clientWidth < 560;
  let vertical = narrow();
  // A full diagram grows with its content (within bounds) instead of shrinking a big cluster to fit a fixed box,
  // so labels stay readable. The stylesheet's height is the minimum.
  const minimum = parseFloat(getComputedStyle(element).height) || 340;
  let fit = 0;
  const onSize = (width, height) => {
    if (current.compact || !width || !height) return;
    const scale = Math.min(1, (element.clientWidth * 0.88) / width);
    const next = `${Math.round(Math.min(Math.max(minimum, height * scale / 0.88), 1100))}px`;
    if (element.style.height === next) return;
    element.style.height = next;
    // React Flow fits the view once when it mounts: remount at the new size.
    fit++;
    render();
  };
  const render = () => {
    element.classList.toggle('topology-compact', current.compact);
    root.render(<StrictMode><Island data={current.data} view={current.view} compact={current.compact} vertical={vertical} onSize={onSize} fit={fit} available={vertical ? Math.max(0, element.clientWidth * 0.88 - 32) : 0} /></StrictMode>);
  };
  const observer = typeof ResizeObserver === 'function'
    ? new ResizeObserver(() => { if (narrow() !== vertical) { vertical = narrow(); render(); } })
    : null;
  observer?.observe(element);
  render();
  return {
    update(nextData, nextOptions = {}) {
      current = { data: nextData ?? current.data, view: nextOptions.view ?? current.view, compact: nextOptions.compact ?? current.compact };
      vertical = narrow();
      render();
    },
    unmount() { observer?.disconnect(); root.unmount(); }
  };
}

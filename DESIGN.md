# Squad

PRODUCT.md owns product truth. This is a GitHub-native **Operate** interface for
people who need to follow app work and understand their team, not a promotional
app-builder landing page.

## Visual system

Primer-inspired semantic color pairs in `wwwroot/app.css`: neutral default/muted
surfaces, blue links and focus rings, green primary actions, bordered sections
and status labels. Light uses white / #f6f8fa / #1f2328; dark uses #0d1117 /
#151b23 / #f0f6fc. Follow system preference with a persisted manual override.
No external fonts, CDN, component package or icon dependency, with one
exception: React Flow (`@xyflow/react`), bundled locally, used only for topology
islands.

System sans-serif body at 14px, 26px app titles, 16px section headings and 12–13px
metadata. Use 4/8px spacing increments, 6px control/section corners and 1px borders.
The custom connected-node mark identifies the team-based app; it does not claim
to be GitHub's logo or an official Squad brand asset.

## Layout and information

A muted global header contains the restrained Squad identity, Apps navigation,
current state and theme switch. Root is a bordered app list with title, status,
latest request and timestamp. New app is an explicit action.

An app has a title, summary and real section-anchor navigation. Desktop uses a
preview/work column plus a 280px team-and-wave sidebar. Requests, previews and
wave results are distinct bordered sections; team names/roles form a compact
list with initials and a member count. Recorded wave contributors appear with
validation and changed-file totals. No fake member status or decorative activity.
Below 700px, everything follows one document column; Team remains reachable
from the section navigation. No nested scroll panels.

## Topology

Topology diagrams follow diagram-design's editorial architecture/deployment
grammar: 1px rules, no shadows, small uppercase mono kind labels, generous
space, and the accent reserved for the entry point and the data store. The
Aspire view reads left to right (front end, API, database); the cluster view
uses one lane per component (Service, workload, volume) inside a dashed
namespace box, with ConfigMaps and Secrets as small chips under their workload.
Narrow containers switch to top-to-bottom. Diagrams are read-only; full ones
carry a screen-reader list, compact ones are decorative thumbnails.

## Behavior

Planning and building reuse the saved brief. Team setup remains automatic,
validated and read-only. A failed roster load has its own visible message and
does not block app work. Wave history and live previews retain their existing
app isolation. Sharing/export uses direct labels rather than metaphorical copy.

Keep keyboard focus visible, preserve labels and alerts, suppress redundant
live announcements, and use a native publication dialog with Escape and focus
restoration. Display bounded model summaries and structured team data through
text nodes; never render native Markdown or raw execution output.

## Reference limits

Public Primer product color and typography foundations informed this
implementation. `github/takeoff` and `github/octave` were requested references
but inaccessible under the current organization's SAML authorization.

# CSV Pipeline — End User License Agreement

Copyright © 2026 toflaks98229. All rights reserved.

> **요약 (한국어 · 참고용)** — 아래 영문이 정본이고, 이 요약은 법적 효력이 없습니다.
> 개발자 1인당 1라이선스입니다. 프로젝트 수에는 제한이 없고, 만들어진 게임은 상업적으로
> 팔 수 있으며, 구워진 에셋과 속성 어셈블리는 빌드에 넣어도 됩니다.
> **소스 코드 자체를 재배포·재판매·공개하는 것만 금지됩니다.**

---

## 0. Which terms apply

**If you obtained this software through the Unity Asset Store**, your use is governed by the
[Unity Asset Store End User License Agreement](https://unity.com/legal/as-terms) ("Asset Store EULA"),
and that agreement prevails over this document wherever the two conflict. This document then serves
only to describe the licensor's intent and to record the permissions in Section 2 that the Asset Store
EULA leaves to the publisher.

**If you obtained this software by any other means** — a direct purchase, an evaluation copy, or a
private distribution from the copyright holder — the terms below are the whole agreement.

## 1. Grant of license

Subject to payment of the applicable fee and to your compliance with this agreement, the copyright
holder grants you a **non-exclusive, non-transferable, non-sublicensable, worldwide license** to use
the Software.

A license is granted **per seat**. One seat covers one natural person. Every individual who uses the
Software — who has it installed, or who edits a project in which it is installed — requires a seat.
Seats may be reassigned between individuals, but not shared concurrently.

An entity that holds *n* seats may install the Software on any number of machines, provided no more
than *n* individuals use it.

## 2. What you may do

- Use the Software in an **unlimited number of projects**, personal or commercial.
- **Distribute the output**: ScriptableObject assets baked by the Software, and the `CsvPipeline`
  runtime assembly (the declarative attributes in `Runtime/`), may be included in the products you
  build and distribute, in compiled form, without royalty or attribution.
- **Modify the Software's source** for your own use within your licensed seats.
- Include the Software in your own **private** version control, including private repositories and
  private package registries accessible only to holders of a valid seat.
- Use the Software in **automated builds and CI** belonging to your organisation.

## 3. What you may not do

- **Redistribute, publish, sell, rent, lease, sublicense, or lend** the Software, in source or
  compiled form, to anyone who does not hold their own seat. This includes committing it to a public
  repository, a public package registry, or any publicly readable location.
- Distribute the Software, modified or not, as part of an **asset, plugin, template, tool, or SDK**
  that you make available to others — whether sold or free.
- Use the Software to build a **competing product** — that is, any tool whose primary purpose is
  importing tabular data into a Unity project.
- Remove, obscure, or alter any copyright, trademark, or license notice contained in the Software.
- Use the Software in violation of applicable law.

Distributing the *output* described in Section 2 is not "distributing the Software" and remains
permitted.

## 4. Ownership

The Software is licensed, not sold. The copyright holder retains all right, title, and interest in
and to the Software, including all intellectual property rights. Nothing in this agreement transfers
ownership. Data you author — your tables, your ScriptableObject types, the assets baked from them —
remains yours entirely.

## 5. Third-party components

The Software contains no third-party code and declares no package dependencies.

## 6. Development assistance disclosure

Parts of this Software were written with the assistance of AI tools. All code has been reviewed by a
human, is delivered unobfuscated and modifiable, and is covered by the automated tests included in
the package.

## 7. Network access

The Software performs no network access unless you create and enable a Google Sheets sync
configuration. When enabled, it contacts only `docs.google.com` and `oauth2.googleapis.com`. It
performs no telemetry or analytics of any kind. Credentials you configure are read from a path you
specify, are never copied into your project, never written to logs, and never transmitted anywhere
other than Google's own token endpoint.

## 8. Term and termination

This license takes effect on delivery and continues until terminated. It terminates automatically
if you materially breach Section 3 and do not cure the breach within thirty (30) days of written
notice. On termination you must stop using the Software and destroy all copies in your possession.

**Products you have already built and shipped are unaffected.** The permissions in Section 2 covering
distributed output survive termination.

## 9. Disclaimer of warranty

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT
LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE, AND NONINFRINGEMENT.

The Software modifies and deletes assets in your Unity project. **You are responsible for keeping your
project under version control.** The Software provides a preview of pending changes and preserves
assets that are still referenced, but these are safeguards, not guarantees.

## 10. Limitation of liability

IN NO EVENT SHALL THE COPYRIGHT HOLDER BE LIABLE FOR ANY CLAIM, DAMAGES, OR OTHER LIABILITY, WHETHER
IN AN ACTION OF CONTRACT, TORT, OR OTHERWISE, ARISING FROM, OUT OF, OR IN CONNECTION WITH THE SOFTWARE
OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

To the maximum extent permitted by applicable law, the copyright holder's total aggregate liability
under this agreement shall not exceed the amount you paid for the license.

Nothing in this agreement excludes or limits liability that cannot lawfully be excluded or limited.

## 11. Governing law

This agreement is governed by the laws of the Republic of Korea, without regard to its conflict of
law provisions. The courts of the Republic of Korea shall have exclusive jurisdiction, except where
mandatory consumer protection law grants you the right to bring proceedings elsewhere.

## 12. Version history

Versions **0.13.2 and earlier** were made available under the MIT License. Those versions remain
under MIT for anyone who lawfully obtained a copy of them. **Version 0.14.0 and later are licensed
solely under this agreement.**

## 13. Support and questions

Support for this package runs through **the reviews section of its Unity Asset Store product page**.
Post there and the publisher answers there, in public, so the answer stays where the next person with
the same question will find it.

If you obtained the Software through the Unity Asset Store, questions about **your licence** — seats,
refunds, invoices — are handled by Unity, not by the publisher, because the Asset Store EULA governs
that relationship (see Section 0). Questions about **the Software itself** belong in the reviews.

If you obtained the Software by any other means, ask wherever you obtained it.


#include "stdafx.h"
#include "BillboardText.h"

using namespace Dynamo::Bloodstone;

// ================================================================================
// BitmapData
// ================================================================================

BitmapData::BitmapData(int width, int height, const unsigned char* pBitmapData) : 
    mPixelWidth(width), mPixelHeight(height), mpBitmapData(pBitmapData)
{
}

int BitmapData::Width() const
{
    return mPixelWidth;
}

int BitmapData::Height() const
{
    return mPixelHeight;
}

const unsigned char* BitmapData::Data() const
{
    return mpBitmapData;
}

// ================================================================================
// GlyphComparer
// ================================================================================

GlyphComparer::GlyphComparer(TextBitmapGenerator* pTextBitmapGenerator)
{
}

bool GlyphComparer::operator()(GlyphId idOne, GlyphId idTwo)
{
    // This method sorts all cached glyphs based on their 
    // font height, larger fonts go before the smaller ones.
    return idOne < idTwo;
}

// ================================================================================
// TextBitmapGenerator
// ================================================================================

// Padding of 2 pixels on each side of a character.
const float TextBitmapGenerator::Margin = 2.0f;

TextBitmapGenerator::TextBitmapGenerator() :
    mContentUpdated(false),
    mCurrentFontId(1024),
    mpGlyphBitmap(nullptr),
    mpCachedGlyphs(nullptr),
    mGlyphComparer(this)
{
    mpCachedGlyphs = new std::map<GlyphId, GlyphMetrics, GlyphComparer>(mGlyphComparer);
}

TextBitmapGenerator::~TextBitmapGenerator()
{
    if (mpGlyphBitmap != nullptr) {
        delete mpGlyphBitmap;
        mpGlyphBitmap = nullptr;
    }

    if (mpCachedGlyphs != nullptr) {
        delete mpCachedGlyphs;
        mpCachedGlyphs = nullptr;
    }
}

FontId TextBitmapGenerator::CacheFont(const FontSpecs& fontSpecs)
{
    auto iterator = mFontSpecs.begin();
    for (; iterator != mFontSpecs.end(); ++iterator)
    {
        if (fontSpecs == iterator->second)
            return iterator->first;
    }

    auto fontId = mCurrentFontId++;
    std::pair<FontId, FontSpecs> pair(fontId, fontSpecs);
    mFontSpecs.insert(pair);

    mContentUpdated = true;
    return pair.first;
}

void TextBitmapGenerator::CacheGlyphs(const std::vector<GlyphId>& glyphs)
{
    auto iterator = glyphs.begin();
    for (; iterator != glyphs.end(); ++iterator)
    {
        // If glyph is not currently cached, add to pending list.
        if (mpCachedGlyphs->find(*iterator) != mpCachedGlyphs->end())
            continue;

        mContentUpdated = true;
        mGlyphsToCache.push_back(*iterator);
    }
}

const FontSpecs& TextBitmapGenerator::GetFontSpecs(FontId fontId) const
{
    // There is no way to have a FontId without a corresponding 
    // entry in the mFontSpecs map, so this is safe access.
    return (mFontSpecs.find(fontId))->second;
}

const BitmapData* TextBitmapGenerator::GenerateBitmap()
{
    if (mContentUpdated == false)
        return mpGlyphBitmap;

    auto iterator = mGlyphsToCache.begin();
    for (; iterator != mGlyphsToCache.end(); ++iterator)
    {
        auto glyphId = *iterator;
        auto metrics = MeasureGlyphCore(glyphId);
        std::pair<GlyphId, GlyphMetrics> pair(glyphId, metrics);
        mpCachedGlyphs->insert(pair);
    }

    mGlyphsToCache.clear(); // Done caching all glyphs.

    int width = 256, height = 256; // Initial size if no bitmap was created.
    if (mpGlyphBitmap == nullptr)
        AllocateBitmapCore(width, height);
    else
    {
        width = mpGlyphBitmap->Width();
        height = mpGlyphBitmap->Height();
    }

    while (true) // Begin placing all glyphs onto the glyph bitmap.
    {
        if (PlaceAllGlyphsOnBitmap(width, height))
            break; // Placed all glyphs, get outta here.

        // Take turn to increase the width and height. If width is larger than 
        // height, then height will be doubled; otherwise, increase the width.
        // 
        if (height > width)
            width = width * 2;
        else
            height = height * 2;

        AllocateBitmapCore(width, height);
    }

    mContentUpdated = false;
    delete mpGlyphBitmap;
    mpGlyphBitmap = new BitmapData(width, height, GetBitmapDataCore());
    return mpGlyphBitmap;
}

bool TextBitmapGenerator::PlaceAllGlyphsOnBitmap(int width, int height) const
{
    float x = 0.0f, y = 0.0f;
    float currentLineHeight = 0.0f;
    const float invWidth = 1.0f / width;
    const float invHeight = 1.0f / height;

    auto iterator = mpCachedGlyphs->begin();
    for (; iterator != mpCachedGlyphs->end(); ++iterator)
    {
        auto gm = iterator->second; // GlyphMetrics
        if (currentLineHeight < gm.extendedHeight)
            currentLineHeight = gm.extendedHeight;

        if (((int)(x + gm.extendedWidth)) > width) {
            x = 0.0f;
            y = y + currentLineHeight;
            currentLineHeight = 0.0f;
        }

        if (((int)(y + gm.extendedHeight)) > height)
            break; // There is no space for this glyph.

        // Render the glyph on the underlying bitmap.
        RenderGlyphParams renderGlyphParams = { 0 };
        renderGlyphParams.x = x;
        renderGlyphParams.y = y;
        renderGlyphParams.glyphId = iterator->first;
        renderGlyphParams.metrics = iterator->second;
        RenderGlyphCore(renderGlyphParams);

        gm.texCoords[0] = x * invWidth; // Left.
        gm.texCoords[1] = y * invHeight; // Top.
        gm.texCoords[2] = ((x + gm.extendedWidth) * invWidth); // Right.
        gm.texCoords[3] = ((y + gm.extendedHeight) * invHeight); // Bottom.
        iterator->second = gm; // Update glyph metrics in the map.

        x = x + gm.extendedWidth; // Advance to the next character.
    }

    return (iterator == mpCachedGlyphs->end()); // Placed all the glyphs?
}

#ifdef _WIN32

// ================================================================================
// TextBitmapGeneratorWin32
// ================================================================================

TextBitmapGeneratorWin32::TextBitmapGeneratorWin32() : 
    mDeviceContext(nullptr),
    mSelectedFont(nullptr),
    mPrevBitmap(nullptr),
    mCurrBitmap(nullptr),
    mpBitmapBits(nullptr),
    mBitmapWidth(0),
    mBitmapHeight(0)
{
    mDeviceContext = ::CreateCompatibleDC(nullptr);
    ::SetBkMode(mDeviceContext, TRANSPARENT);
    ::SetTextColor(mDeviceContext, RGB(0xff, 0xff, 0xff));
}

TextBitmapGeneratorWin32::~TextBitmapGeneratorWin32()
{
    mpBitmapBits = nullptr; // Buffer owned by mCurrBitmap.

    if (mSelectedFont != nullptr) {
        ::SelectObject(mDeviceContext, mSelectedFont);
        mSelectedFont = nullptr;
    }

    auto iterator = mFontResources.begin();
    for (; iterator != mFontResources.end(); ++ iterator)
        ::DeleteObject(iterator->second);

    mFontResources.clear();

    if (mPrevBitmap != nullptr) {
        ::SelectObject(mDeviceContext, mPrevBitmap);
        mPrevBitmap = nullptr;
    }

    if (mCurrBitmap != nullptr) {
        ::DeleteObject(mCurrBitmap);
        mCurrBitmap = nullptr;
    }

    if (mDeviceContext != nullptr) {
        ::DeleteDC(mDeviceContext);
        mDeviceContext = nullptr;
    }
}

GlyphMetrics TextBitmapGeneratorWin32::MeasureGlyphCore(GlyphId glyphId)
{
    EnsureFontSelected(EnsureFontResourceLoaded(glyphId));

    TEXTMETRIC textMetrics = { 0 };
    ::GetTextMetrics(mDeviceContext, &textMetrics);
    const auto height = textMetrics.tmHeight - textMetrics.tmInternalLeading;

    const auto c = GETCHARACTER(glyphId);
    ABCFLOAT widths = { 0 };
    ::GetCharABCWidthsFloat(mDeviceContext, c, c, &widths);

    GlyphMetrics glyphMetrics = { 0 };
    glyphMetrics.characterWidth = widths.abcfB;
    glyphMetrics.characterHeight = ((float) height);
    glyphMetrics.advance = widths.abcfA + widths.abcfB + widths.abcfC;

    // Offset for the actual glyph rendering w.r.t. top/left corner.
    glyphMetrics.horzRenderOffset = TextBitmapGenerator::Margin;
    glyphMetrics.vertRenderOffset = TextBitmapGenerator::Margin;
    glyphMetrics.horzRenderOffset -= ((float) widths.abcfA);
    glyphMetrics.vertRenderOffset -= ((float) textMetrics.tmInternalLeading);

    // Add extra padding around the character.
    const auto margins = TextBitmapGenerator::Margin * 2.0f;
    glyphMetrics.extendedWidth = glyphMetrics.characterWidth + margins;
    glyphMetrics.extendedHeight = glyphMetrics.characterHeight + margins;
    return glyphMetrics;
}

bool TextBitmapGeneratorWin32::AllocateBitmapCore(int width, int height)
{
    if (width == mBitmapWidth && (height == mBitmapHeight))
        return true; // Requested to create bitmap of same size.

    mBitmapWidth = width;
    mBitmapHeight = height;

    // Destroy existing, if any.
    if (mCurrBitmap != nullptr) {
        SelectObject(mDeviceContext, mPrevBitmap);
        DeleteObject(mCurrBitmap);
        mCurrBitmap = nullptr;
    }

    BITMAPINFO bitmapInfo = { 0 };
    bitmapInfo.bmiHeader.biSize = sizeof(BITMAPINFOHEADER);
    bitmapInfo.bmiHeader.biWidth = mBitmapWidth;
    bitmapInfo.bmiHeader.biHeight = mBitmapHeight;
    bitmapInfo.bmiHeader.biPlanes = 1;
    bitmapInfo.bmiHeader.biBitCount = 32;
    bitmapInfo.bmiHeader.biCompression = BI_RGB;

    mpBitmapBits = nullptr;
    mCurrBitmap = CreateDIBSection(mDeviceContext, &bitmapInfo,
        DIB_RGB_COLORS, ((void **) &mpBitmapBits), nullptr, 0);

    mPrevBitmap = ((HBITMAP) ::SelectObject(mDeviceContext, mCurrBitmap));
    return true;
}

void TextBitmapGeneratorWin32::RenderGlyphCore(const RenderGlyphParams& params) const
{
    auto fontSpecs = GetFontSpecs(GETFONTID(params.glyphId));
    auto iterator = mFontResources.find(fontSpecs.face);
    auto pThis = const_cast<TextBitmapGeneratorWin32 *>(this);
    pThis->EnsureFontSelected(iterator->second);

    const auto x = params.x + params.metrics.horzRenderOffset;
    const auto y = params.y + params.metrics.vertRenderOffset;
    const auto character = GETCHARACTER(params.glyphId);
    TextOut(mDeviceContext, ((int) x), ((int) y), &character, 1);
}

const unsigned char* TextBitmapGeneratorWin32::GetBitmapDataCore(void) const
{
    return mpBitmapBits;
}

void TextBitmapGeneratorWin32::EnsureFontSelected(HFONT fontForGlyph)
{
    if (fontForGlyph != mSelectedFont) {
        ::SelectObject(mDeviceContext, fontForGlyph);
        mSelectedFont = fontForGlyph;
    }
}

HFONT TextBitmapGeneratorWin32::EnsureFontResourceLoaded(GlyphId glyphId)
{
    auto fontSpecs = GetFontSpecs(GETFONTID(glyphId));
    auto iterator = mFontResources.find(fontSpecs.face);
    if (iterator != mFontResources.end())
        return iterator->second;

    LOGFONT lf = { 0 };
    lf.lfHeight = fontSpecs.height;
    lf.lfWeight = FW_NORMAL;
    lf.lfCharSet = DEFAULT_CHARSET;
    lf.lfQuality = CLEARTYPE_QUALITY;

    if (HASFLAG(fontSpecs.flags, FontFlags::Bold))
        lf.lfWeight = FW_BOLD;
    else if (HASFLAG(fontSpecs.flags, FontFlags::Thin))
        lf.lfWeight = FW_THIN;

    if (HASFLAG(fontSpecs.flags, FontFlags::Italic))
        lf.lfItalic = TRUE;
    if (HASFLAG(fontSpecs.flags, FontFlags::Underline))
        lf.lfUnderline = TRUE;
    if (HASFLAG(fontSpecs.flags, FontFlags::StrikeOut))
        lf.lfStrikeOut = TRUE;

    wcscpy_s(lf.lfFaceName, fontSpecs.face.c_str());
    auto hfont = ::CreateFontIndirect(&lf);
    std::pair<std::wstring, HFONT> pair(fontSpecs.face, hfont);
    mFontResources.insert(pair);
    return pair.second;
}

TextBitmapGenerator* CreateTextBitmapGenerator(void)
{
    return new TextBitmapGeneratorWin32();
}

#endif

BillboardText::BillboardText(TextId textId, FontId fontId) : 
    mTextId(textId),
    mFontId(fontId)
{
    mForegroundRgba0[0] = mForegroundRgba0[1] = mForegroundRgba0[2] = 1.0f;
    mForegroundRgba1[0] = mForegroundRgba1[1] = mForegroundRgba1[2] = 1.0f;
    mBackgroundRgba[0] = mBackgroundRgba[1] = mBackgroundRgba[2] = 0.0f;

    // Alpha for both foreground and background colors.
    mForegroundRgba0[3] = mForegroundRgba1[3] = mBackgroundRgba[3] = 1.0f;
}

TextId BillboardText::GetTextId(void) const
{
    return this->mTextId;
}

FontId BillboardText::GetFontId(void) const
{
    return this->mFontId;
}

const std::vector<GlyphId>& BillboardText::GetGlyphs(void) const
{
    return this->mTextContent;
}

void BillboardText::Update(const std::wstring& content)
{
    mTextContent.clear(); // Clear the existing content first.

    auto iterator = content.begin();
    for (; iterator != content.end(); ++iterator) {
        wchar_t character = *iterator;
        mTextContent.push_back(MAKEGLYPHID(mFontId, character));
    }
}

void BillboardText::Update(const float* position)
{
    for (int i = 0; i < 4; i++)
        mWorldPosition[i] = position[i];
}

void BillboardText::UpdateForeground0(const float* rgba)
{
    for (int i = 0; i < 4; i++)
        mForegroundRgba0[i] = rgba[i];
}

void BillboardText::UpdateForeground1(const float* rgba)
{
    for (int i = 0; i < 4; i++)
        mForegroundRgba1[i] = rgba[i];
}

void BillboardText::UpdateBackground(const float* rgba)
{
    for (int i = 0; i < 4; i++)
        mBackgroundRgba[i] = rgba[i];
}

#ifdef BLOODSTONE_EXPORTS

// ================================================================================
// BillboardTextGroup
// ================================================================================

BillboardTextGroup::BillboardTextGroup(IGraphicsContext* pGraphicsContext) : 
    mScreenSizeParamIndex(-1),
    mRegenerationHints(RegenerationHints::None),
    mCurrentTextId(1024),
    mpGraphicsContext(pGraphicsContext),
    mpVertexBuffer(nullptr),
    mpBillboardShader(nullptr),
    mpBitmapGenerator(nullptr)
{
    mpBitmapGenerator = CreateTextBitmapGenerator();
}

BillboardTextGroup::~BillboardTextGroup()
{
    auto iterator = mBillboardTexts.begin();
    for (; iterator != mBillboardTexts.end(); ++iterator)
        delete ((BillboardText *)(iterator->second));

    mBillboardTexts.clear();

    if (mpBillboardShader != nullptr) {
        delete mpBillboardShader;
        mpBillboardShader = nullptr;
    }

    if (mpVertexBuffer != nullptr) {
        delete mpVertexBuffer;
        mpVertexBuffer = nullptr;
    }
}

TextId BillboardTextGroup::CreateText(const FontSpecs& fontSpecs)
{
    auto textId = mCurrentTextId++;
    auto fontId = mpBitmapGenerator->CacheFont(fontSpecs);
    auto pBillboardText = new BillboardText(textId, fontId);

    // Insert the newly created billboard text into the internal list.
    std::pair<TextId, BillboardText*> pair(textId, pBillboardText);
    mBillboardTexts.insert(pair);
    ADDFLAG(mRegenerationHints, RegenerationHints::TextureContent);
    return textId;
}

void BillboardTextGroup::Destroy(TextId textId)
{
    auto iterator = mBillboardTexts.find(textId);
    if (iterator == mBillboardTexts.end())
        return; // Text not found.

    mBillboardTexts.erase(iterator);
    delete ((BillboardText *)(iterator->second));
    ADDFLAG(mRegenerationHints, RegenerationHints::VertexBufferLayout);
}

void BillboardTextGroup::Render(void) const
{
    if (mBillboardTexts.size() <- 0) // Nothing to render, forget it.
        return;

    if (nullptr == mpVertexBuffer) {
        auto pThis = const_cast<BillboardTextGroup *>(this);
        pThis->Initialize();
    }

    if (mRegenerationHints != RegenerationHints::None) {
        auto pThis = const_cast<BillboardTextGroup *>(this);
        pThis->RegenerateInternal();
    }

    mpGraphicsContext->ActivateShaderProgram(mpBillboardShader);

    auto pCamera = mpGraphicsContext->GetDefaultCamera();
    mpBillboardShader->ApplyTransformation(pCamera);

    int width = 0, height = 0;
    mpGraphicsContext->GetDisplayPixelSize(width, height);
    float screenSize[] = { ((float) width), ((float) height) };
    mpBillboardShader->SetParameter(mScreenSizeParamIndex, screenSize, 2);

    mpVertexBuffer->Render();
}

void BillboardTextGroup::UpdateText(TextId textId, const std::wstring& text)
{
    auto pBillboardText = GetBillboardText(textId);
    if (pBillboardText != nullptr) {
        pBillboardText->Update(text);
        mpBitmapGenerator->CacheGlyphs(pBillboardText->GetGlyphs());
        ADDFLAG(mRegenerationHints, RegenerationHints::All);
    }
}

void BillboardTextGroup::UpdatePosition(TextId textId, const float* position)
{
    auto pBillboardText = GetBillboardText(textId);
    if (pBillboardText != nullptr) {
        pBillboardText->Update(position);
        ADDFLAG(mRegenerationHints, RegenerationHints::VertexBufferContent);
    }
}

void BillboardTextGroup::UpdateColor(TextId textId,
    const float* foregroundRgba,
    const float* backgroundRgba)
{
    auto pBillboardText = GetBillboardText(textId);
    if (pBillboardText == nullptr)
        return;

    pBillboardText->UpdateForeground0(foregroundRgba);
    pBillboardText->UpdateForeground1(foregroundRgba);
    pBillboardText->UpdateBackground(backgroundRgba);
    ADDFLAG(mRegenerationHints, RegenerationHints::VertexBufferContent);
}

void BillboardTextGroup::UpdateColor(TextId textId,
    const float* foregroundRgba0,
    const float* foregroundRgba1,
    const float* backgroundRgba)
{
    auto pBillboardText = GetBillboardText(textId);
    if (pBillboardText == nullptr)
        return;

    pBillboardText->UpdateForeground0(foregroundRgba0);
    pBillboardText->UpdateForeground1(foregroundRgba1);
    pBillboardText->UpdateBackground(backgroundRgba);
    ADDFLAG(mRegenerationHints, RegenerationHints::VertexBufferContent);
}

BillboardText* BillboardTextGroup::GetBillboardText(TextId textId) const
{
    auto iterator = mBillboardTexts.find(textId);
    return ((iterator == mBillboardTexts.end()) ? nullptr : iterator->second);
}

void BillboardTextGroup::Initialize(void)
{
    if (nullptr == mpVertexBuffer)
        mpVertexBuffer = mpGraphicsContext->CreateBillboardVertexBuffer();

    if (nullptr == mpBillboardShader) {
        auto name = ShaderName::BillboardText;
        mpBillboardShader = mpGraphicsContext->CreateShaderProgram(name);
    }

    mpBillboardShader->BindTransformMatrix(TransMatrix::Model, "model");
    mpBillboardShader->BindTransformMatrix(TransMatrix::View, "view");
    mpBillboardShader->BindTransformMatrix(TransMatrix::Projection, "proj");
    mScreenSizeParamIndex = mpBillboardShader->GetShaderParameterIndex("screenSize");
    mpVertexBuffer->BindToShaderProgram(mpBillboardShader);
}

void BillboardTextGroup::RegenerateInternal(void)
{
    if (HASFLAG(mRegenerationHints, RegenerationHints::TextureContent))
        RegenerateTexture();
    if (HASFLAG(mRegenerationHints, RegenerationHints::VertexBufferLayout))
        RegenerateVertexBuffer();
    if (HASFLAG(mRegenerationHints, RegenerationHints::VertexBufferContent))
        UpdateVertexBuffer();

    mRegenerationHints = RegenerationHints::None; // Clear all.
}

void BillboardTextGroup::RegenerateTexture(void)
{
}

void BillboardTextGroup::RegenerateVertexBuffer(void)
{
}

void BillboardTextGroup::UpdateVertexBuffer(void)
{
    const float position[]  = { 0.0f, 0.0f, 0.0f };
    const float texCoords[] = { 0.0f, 1.0f, 1.0f, 0.0f };
    const float offset[]    = { 0.0f, 16.0f, 64.0f, 0.0f };
    const float rgba0[]     = { 0.0f, 1.0f, 0.0f, 1.0f };
    const float rgba1[]     = { 0.0f, 0.0f, 1.0f, 1.0f };

    std::vector<BillboardVertex> vertices;
    BillboardQuadInfo quadInfo(vertices);
    quadInfo.position3 = position;
    quadInfo.offset4 = offset;
    quadInfo.texCoords4 = texCoords;
    quadInfo.foregroundRgba0 = rgba0;
    quadInfo.foregroundRgba1 = rgba1;
    FillQuad(quadInfo);

    mpVertexBuffer->Update(vertices);
}

void BillboardTextGroup::FillQuad(const BillboardQuadInfo& quadInfo) const
{
    const float* pos    = quadInfo.position3;
    const float* rgba0  = quadInfo.foregroundRgba0;
    const float* rgba1  = quadInfo.foregroundRgba1;
    const float* tc     = quadInfo.texCoords4;
    const float* off    = quadInfo.offset4;

    // Left-top vertex.
    const float tc0[] = { tc[0], tc[1] };
    const float off0[] = { off[0], off[1] };
    BillboardVertex lt(pos, &tc0[0], &off0[0], rgba0);

    // Right-top vertex.
    const float tc1[] = { tc[2], tc[1] };
    const float off1[] = { off[2], off[1] };
    BillboardVertex rt(pos, &tc1[0], &off1[0], rgba0);

    // Left-bottom vertex.
    const float tc2[] = { tc[0], tc[3] };
    const float off2[] = { off[0], off[3] };
    BillboardVertex lb(pos, &tc2[0], &off2[0], rgba1);

    // Right-bottom vertex.
    const float tc3[] = { tc[2], tc[3] };
    const float off3[] = { off[2], off[3] };
    BillboardVertex rb(pos, &tc3[0], &off3[0], rgba1);

    quadInfo.vertices.push_back(lt); // First triangle.
    quadInfo.vertices.push_back(rt);
    quadInfo.vertices.push_back(lb);
    quadInfo.vertices.push_back(lb); // Second triangle.
    quadInfo.vertices.push_back(rt);
    quadInfo.vertices.push_back(rb);
}

#endif // BLOODSTONE_EXPORTS

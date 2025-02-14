
#ifndef _BILLBOARD_TEXT_H_
#define _BILLBOARD_TEXT_H_

#ifdef BLOODSTONE_EXPORTS
#include "Interfaces.h"
#endif

#include <string>
#include <vector>
#include <map>

#define MAKEGLYPHID(fid, c) (((fid & 0x0000ffff) << 16) | (c & 0x0000ffff))
#define GETFONTID(gid)      ((FontId)((gid & 0xffff0000) >> 16))
#define GETCHARACTER(gid)   ((wchar_t)(gid & 0x0000ffff))
#define ADDFLAG(c, n)       (c = ((RegenerationHints)(c | n)))
#define HASFLAG(c, f)       ((c & f) != 0)

namespace Dynamo { namespace Bloodstone {

    class TextBitmapGenerator; // Forward declaration.

    typedef unsigned int    TextId;
    typedef unsigned short  FontId;
    typedef unsigned int    GlyphId;

    enum FontFlags : unsigned short
    {
        Thin      = 0x0001,
        Bold      = 0x0002,
        Italic    = 0x0004,
        Underline = 0x0008,
        StrikeOut = 0x0010
    };

    struct FontSpecs
    {
        int height;
        FontFlags flags;
        std::wstring face;

        FontSpecs(const wchar_t* face)
        {
            this->height = 72;
            this->flags = ((FontFlags) 0);
            this->face = face;
        }

        inline bool operator==(const FontSpecs& other) const
        {
            if (height != other.height || (flags != other.flags))
                return false;

            return this->face == other.face;
        }
    };

    struct GlyphMetrics
    {
        float characterWidth;
        float characterHeight;
        float extendedWidth;
        float extendedHeight;
        float horzRenderOffset;
        float vertRenderOffset;
        float texCoords[4];
        float advance;
    };

    class BitmapData
    {
    public:
        BitmapData(int width, int height, const unsigned char* pBitmapData);
        int Width() const;
        int Height() const;
        const unsigned char* Data() const;

    private:
        int mPixelWidth, mPixelHeight;
        const unsigned char* mpBitmapData;
    };

    struct GlyphComparer : public std::binary_function<GlyphId, GlyphId, bool>
    {
        GlyphComparer() {} // Default constructor, used by STL.
        GlyphComparer(TextBitmapGenerator* pTextBitmapGenerator);
        bool operator()(GlyphId idOne, GlyphId idTwo);
    };

    struct RenderGlyphParams
    {
        float x, y;
        GlyphId glyphId;
        GlyphMetrics metrics;
    };

    class TextBitmapGenerator
    {
    public:
        TextBitmapGenerator();
        virtual ~TextBitmapGenerator();

        FontId CacheFont(const FontSpecs& fontSpecs);
        void CacheGlyphs(const std::vector<GlyphId>& glyphs);
        const FontSpecs& GetFontSpecs(FontId fontId) const;
        const BitmapData* GenerateBitmap();

    protected:
        virtual GlyphMetrics MeasureGlyphCore(GlyphId glyphId) = 0;
        virtual bool AllocateBitmapCore(int width, int height) = 0;
        virtual void RenderGlyphCore(const RenderGlyphParams& params) const = 0;
        virtual const unsigned char* GetBitmapDataCore(void) const = 0;

        const static float Margin;

    private:
        bool PlaceAllGlyphsOnBitmap(int width, int height) const;

        bool mContentUpdated;
        FontId mCurrentFontId;
        BitmapData* mpGlyphBitmap;
        GlyphComparer mGlyphComparer;
        std::vector<GlyphId> mGlyphsToCache;
        std::map<FontId, FontSpecs> mFontSpecs;
        std::map<GlyphId, GlyphMetrics, GlyphComparer>* mpCachedGlyphs;
    };

#ifdef _WIN32

    class TextBitmapGeneratorWin32 : public TextBitmapGenerator
    {
    public:
        TextBitmapGeneratorWin32();
        ~TextBitmapGeneratorWin32();

    protected:
        virtual GlyphMetrics MeasureGlyphCore(GlyphId glyphId);
        virtual bool AllocateBitmapCore(int width, int height);
        virtual void RenderGlyphCore(const RenderGlyphParams& params) const;
        virtual const unsigned char* GetBitmapDataCore(void) const;

    private:
        void CreateBitmap(int width, int height);
        void EnsureFontSelected(HFONT fontForGlyph);
        HFONT EnsureFontResourceLoaded(GlyphId glyphId);

        HDC mDeviceContext;
        HFONT mSelectedFont;
        HBITMAP mPrevBitmap, mCurrBitmap;
        std::map<std::wstring, HFONT> mFontResources;

        int mBitmapWidth, mBitmapHeight;
        unsigned char* mpBitmapBits;
    };

#endif

    enum RegenerationHints
    {
        None                = 0x00000000,
        VertexBufferContent = 0x00000001,
        VertexBufferLayout  = 0x00000002 | VertexBufferContent,
        TextureContent      = 0x00000004 | VertexBufferLayout,

        All = TextureContent | VertexBufferLayout | VertexBufferContent
    };

    class BillboardText
    {
    public:
        BillboardText(TextId textId, FontId fontId);
        TextId GetTextId(void) const;
        FontId GetFontId(void) const;
        const std::vector<GlyphId>& GetGlyphs(void) const;

        void Update(const std::wstring& content);
        void Update(const float* position);
        void UpdateForeground0(const float* rgba);
        void UpdateForeground1(const float* rgba);
        void UpdateBackground(const float* rgba);

    private:
        TextId mTextId;
        FontId mFontId;
        std::vector<GlyphId> mTextContent;
        float mForegroundRgba0[4];  // Top foreground color.
        float mForegroundRgba1[4];  // Bottom foreground color.
        float mBackgroundRgba[4];   // Background shadow color.
        float mWorldPosition[4];    // 4th entry ignored by vertex shader.
    };

#ifdef BLOODSTONE_EXPORTS

    struct BillboardQuadInfo
    {
        BillboardQuadInfo(std::vector<BillboardVertex>& bvs) :
            vertices(bvs)
        {
        }

        const float* position3;         // Base point world position.
        const float* offset4;           // Left, top, right, bottom.
        const float* texCoords4;        // Left, top, right, bottom.
        const float* foregroundRgba0;   // Top foreground color.
        const float* foregroundRgba1;   // Bottom foreground color.
        std::vector<BillboardVertex>& vertices;
    };

    class BillboardTextGroup
    {
    public:
        BillboardTextGroup(IGraphicsContext* pGraphicsContext);
        ~BillboardTextGroup();

        TextId CreateText(const FontSpecs& fontSpecs);
        void Destroy(TextId textId);
        void Render(void) const;
        void UpdateText(TextId textId,
            const std::wstring& text);
        void UpdatePosition(TextId textId,
            const float* position);
        void UpdateColor(TextId textId,
            const float* foregroundRgba,
            const float* backgroundRgba);
        void UpdateColor(TextId textId,
            const float* foregroundRgba0,
            const float* foregroundRgba1,
            const float* backgroundRgba);

    private:

        BillboardText* GetBillboardText(TextId textId) const;
        void Initialize(void);
        void RegenerateInternal(void);
        void RegenerateTexture(void);
        void RegenerateVertexBuffer(void);
        void UpdateVertexBuffer(void);

        // TODO: Move this to BillboardText class as private.
        void FillQuad(const BillboardQuadInfo& quadInfo) const;

        TextId mCurrentTextId;
        std::map<TextId, BillboardText*> mBillboardTexts;

        // Shader parameter indices.
        int mScreenSizeParamIndex;

        RegenerationHints mRegenerationHints;
        IBillboardVertexBuffer* mpVertexBuffer;
        IShaderProgram* mpBillboardShader;
        TextBitmapGenerator* mpBitmapGenerator;
        IGraphicsContext* mpGraphicsContext;
    };

#endif // BLOODSTONE_EXPORTS

} }

#endif
